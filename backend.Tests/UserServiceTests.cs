using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Models;
using Data;
using Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hubs;
using Microsoft.AspNetCore.SignalR;
using FakeItEasy;
using System.Security.Cryptography;

namespace backend.Tests
{
    public class UserServiceTests
    {
        private readonly GameDbContext _context;
        private readonly UserService _service;
        private readonly RoomService _roomService;

        public UserServiceTests()
        {
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();

            _service = new UserService(_context);

            IHubContext<SpectatorHub> hubContext = A.Fake<IHubContext<SpectatorHub>>();
            _roomService = new RoomService(hubContext);
        }


        [Theory]
        [InlineData("robert", "hash123")]
        [InlineData("alice", "hash456")]
        [InlineData("bob", "hash789")]
        public async Task GetUserAsync_Should_Return_User_When_Found(string username, string passwordHash)
        {
            // Arrange
            var user = new User { Username = username, PasswordHash = passwordHash };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetUserAsync(username);

            // Assert
            result.Should().NotBeNull();
            result!.Username.Should().Be(username);
            result.PasswordHash.Should().Be(passwordHash);
        }

        [Fact]
        public async Task SearchUsersAsync_Should_ReturnEmptyList_When_QueryIsNull()
        {
            var result = await _service.SearchUsersAsync("");

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchUsersAsync_Should_Return_Matching_Users()
        {
            _context.Users.AddRange(
                new User { Username = "robert", PasswordHash = "hash123" },
                new User { Username = "robocop", PasswordHash = "hash123" },
                new User { Username = "alice", PasswordHash = "hash123" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.SearchUsersAsync("rob");

            result.Should().HaveCount(2);
            result.Select(u => u.Username).Should().Contain(new[] { "robert", "robocop" });
        }

        [Fact]
        public async Task RegisterUserAsync_Should_Add_User_When_Not_Exists()
        {
            var result = await _service.RegisterUserAsync("newuser", "password123");

            result.Should().BeTrue();
            var user = await _context.Users.FindAsync("newuser");
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task RegisterUserAsync_Should_Return_False_When_User_Already_Exists()
        {
            var existingUser = new User
            {
                Username = "existinguser",
                PasswordHash = "hash123"
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var result = await _service.RegisterUserAsync("existinguser", "newpassword");

            result.Should().BeFalse();

            var usersWithSameUsername = await _context.Users.CountAsync(u => u.Username == "existinguser");
            usersWithSameUsername.Should().Be(1);
        }

        [Fact]
        public void GenerateJwtToken_Should_Return_Valid_Jwt_For_User()
        {
            var user = new User { Username = "robert", PasswordHash = "hash123" };

            var tokenString = _service.GenerateJwtToken(user);

            tokenString.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            var nameClaim = token.Claims.FirstOrDefault(c => c.Type == "unique_name");
            nameClaim.Should().NotBeNull();
            nameClaim!.Value.Should().Be("robert");

            token.ValidTo.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task GetUserFromTokenAsync_Should_Return_User_When_Token_Is_Valid()
        {
            var username = "robert";
            var passwordHash = "hash123";
            var user = new User { Username = username, PasswordHash = passwordHash };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _service.GenerateJwtToken(user);

            var result = await _service.GetUserFromTokenAsync(token);

            result.Should().NotBeNull();
            result!.Username.Should().Be(username);
            result.PasswordHash.Should().Be(passwordHash);
        }

        [Fact]
        public async Task GetUserFromTokenAsync_Should_Return_Null_When_Token_Has_Invalid_Signature()
        {
            var fakeKey = Encoding.ASCII.GetBytes("this-is-a-very-long-fake-secret-key-123!");
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "robert") }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(fakeKey), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = tokenHandler.WriteToken(token);

            var result = await _service.GetUserFromTokenAsync(jwtToken);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetUserFromTokenAsync_Should_Return_Null_When_Token_Has_No_Username_Claim()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(UserService.KEY), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = tokenHandler.WriteToken(token);

            var result = await _service.GetUserFromTokenAsync(jwtToken);

            result.Should().BeNull();
        }

        [Fact]
        public async Task DeleteUserAsync_Should_Return_True_When_User_Exists()
        {
            _context.Users.Add(new User { Username = "testuser", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.DeleteUserAsync("testuser");

            result.Should().BeTrue();
            var deletedUser = await _context.Users.FindAsync("testuser");
            deletedUser.Should().BeNull();
        }

        [Fact]
        public async Task DeleteUserAsync_Should_Return_False_When_User_Does_Not_Exist()
        {
            var result = await _service.DeleteUserAsync("nonexistent");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendFriendRequestAsync_Should_ReturnFalse_When_FriendingYourself()
        {
            var result = await _service.SendFriendRequestAsync("alice", "alice");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task SendFriendRequestAsync_Should_ReturnFalse_When_UserOrTargetDoesNotExist()
        {
            _context.Users.Add(new User { Username = "alice", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.SendFriendRequestAsync("alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task AcceptFriendRequestAsync_Should_ReturnFalse_When_UserOrRequesterDoesNotExist()
        {
            _context.Users.Add(new User { Username = "alice", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.AcceptFriendRequestAsync("alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task AcceptFriendRequestAsync_Should_ReturnFalse_When_NoIncomingRequestExists()
        {
            _context.Users.AddRange(
                new User { Username = "alice", PasswordHash = "hash123" },
                new User { Username = "bob", PasswordHash = "hash456" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.AcceptFriendRequestAsync("alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task AcceptFriendRequestAsync_Should_AddUsersAsFriends_When_RequestExists()
        {
            _context.Users.AddRange(
                new User
                {
                    Username = "alice",
                    PasswordHash = "hash123",
                    IncomingFriendRequests = new List<string> { "bob" },
                    Friends = new List<string>()
                },
                new User
                {
                    Username = "bob",
                    PasswordHash = "hash456",
                    OutgoingFriendRequests = new List<string> { "alice" },
                    Friends = new List<string>()
                }
            );
            await _context.SaveChangesAsync();

            var result = await _service.AcceptFriendRequestAsync("alice", "bob");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");
            var bob = await _context.Users.FindAsync("bob");

            if (alice == null || bob == null)
            {
                throw new Exception("Users should exist in the database.");
            }

            alice.Friends.Should().Contain("bob");
            bob.Friends.Should().Contain("alice");
            alice.IncomingFriendRequests.Should().NotContain("bob");
            bob.OutgoingFriendRequests.Should().NotContain("alice");
        }

        [Fact]
        public async Task RejectFriendRequestAsync_Should_ReturnFalse_When_UserOrRequesterDoesNotExist()
        {
            _context.Users.Add(new User { Username = "alice", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.RejectFriendRequestAsync("alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task RejectFriendRequestAsync_Should_ReturnFalse_When_NoIncomingRequestExists()
        {
            _context.Users.AddRange(
                new User { Username = "alice", PasswordHash = "hash123" },
                new User { Username = "bob", PasswordHash = "hash456" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.RejectFriendRequestAsync("alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task RejectFriendRequestAsync_Should_RemoveRequestAndReturnTrue_When_RequestExists()
        {
            _context.Users.AddRange(
                new User
                {
                    Username = "alice",
                    PasswordHash = "hash123",
                    IncomingFriendRequests = new List<string> { "bob" }
                },
                new User
                {
                    Username = "bob",
                    PasswordHash = "hash456",
                    OutgoingFriendRequests = new List<string> { "alice" }
                }
            );
            await _context.SaveChangesAsync();

            var result = await _service.RejectFriendRequestAsync("alice", "bob");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");
            var bob = await _context.Users.FindAsync("bob");

            if (alice == null || bob == null)
            {
                throw new Exception("Users should exist in the database.");
            }

            alice.IncomingFriendRequests.Should().NotContain("bob");
            bob.OutgoingFriendRequests.Should().NotContain("alice");
        }

        [Fact]
        public async Task RemoveFriendAsync_Should_ReturnFalse_When_UserOrFriendDoesNotExist()
        {
            _context.Users.Add(new User { Username = "alice", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.RemoveFriendAsync("alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveFriendAsync_Should_ReturnTrue_When_UsersExist_But_NotFriends()
        {
            _context.Users.AddRange(
                new User { Username = "alice", PasswordHash = "hash123" },
                new User { Username = "bob", PasswordHash = "hash456" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.RemoveFriendAsync("alice", "bob");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task RemoveFriendAsync_Should_RemoveUsersFromEachOthersFriendsList_When_TheyAreFriends()
        {
            _context.Users.AddRange(
                new User
                {
                    Username = "alice",
                    PasswordHash = "hash123",
                    Friends = new List<string> { "bob" }
                },
                new User
                {
                    Username = "bob",
                    PasswordHash = "hash456",
                    Friends = new List<string> { "alice" }
                }
            );
            await _context.SaveChangesAsync();

            var result = await _service.RemoveFriendAsync("alice", "bob");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");
            var bob = await _context.Users.FindAsync("bob");

            if (alice == null || bob == null)
            {
                throw new Exception("Users should exist in the database.");
            }

            alice.Friends.Should().NotContain("bob");
            bob.Friends.Should().NotContain("alice");
        }

        [Fact]
        public async Task InviteFriendToGame_Should_ReturnFalse_When_UserOrTargetDoesNotExist()
        {
            _context.Users.Add(new User { Username = "alice", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.InviteFriendToGame("alice", "bob", "pair-matching", "HJF534");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task InviteFriendToGame_Should_ReturnFalse_When_UsersAreNotFriends()
        {
            _context.Users.AddRange(
                new User { Username = "alice", PasswordHash = "hash123" },
                new User { Username = "bob", PasswordHash = "hash456" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.InviteFriendToGame("alice", "bob", "four-in-a-row", "J843HJ");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task InviteFriendToGame_Should_AddInvitation_When_UsersAreFriends()
        {
            _context.Users.AddRange(
                new User
                {
                    Username = "alice",
                    PasswordHash = "hash123",
                    Friends = new List<string> { "bob" },
                    OutcomingInviteToGameRequests = new List<ToInvitationToGame>()
                },
                new User
                {
                    Username = "bob",
                    PasswordHash = "hash456",
                    Friends = new List<string> { "alice" },
                    IncomingInviteToGameRequests = new List<FromInvitationToGame>()
                }
            );
            await _context.SaveChangesAsync();

            var result = await _service.InviteFriendToGame("alice", "bob", "pair-matching", "J843HJ");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");
            var bob = await _context.Users.FindAsync("bob");

            if (alice == null || bob == null)
            {
                throw new Exception("Users should exist in the database.");
            }

            alice.OutcomingInviteToGameRequests.Should().ContainSingle(i => i.ToUsername == "bob" && i.RoomKey == "pair-matching:J843HJ");
            bob.IncomingInviteToGameRequests.Should().ContainSingle(i => i.FromUsername == "alice" && i.RoomKey == "pair-matching:J843HJ");
        }

        [Fact]
        public async Task RemoveInviteFriendToGame_Should_ReturnFalse_When_UserOrTargetDoesNotExist()
        {
            _context.Users.Add(new User { Username = "alice", PasswordHash = "hash123" });
            await _context.SaveChangesAsync();

            var result = await _service.RemoveInviteFriendToGame("alice", "bob", "pair-matching", "J843HJ");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveInviteFriendToGame_Should_RemoveInvitations_When_TheyExist()
        {
            var roomKey = "pair-matching:J843HJ";

            _context.Users.AddRange(
                new User
                {
                    Username = "alice",
                    PasswordHash = "hash123",
                    OutcomingInviteToGameRequests = new List<ToInvitationToGame>
                    {
                        new ToInvitationToGame("bob", roomKey)
                    }
                },
                new User
                {
                    Username = "bob",
                    PasswordHash = "hash456",
                    IncomingInviteToGameRequests = new List<FromInvitationToGame>
                    {
                        new FromInvitationToGame("alice", roomKey)
                    }
                }
            );
            await _context.SaveChangesAsync();

            var result = await _service.RemoveInviteFriendToGame("alice", "bob", "pair-matching", "J843HJ");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");
            var bob = await _context.Users.FindAsync("bob");

            if (alice == null || bob == null)
            {
                throw new Exception("Users should exist in the database.");
            }

            alice.OutcomingInviteToGameRequests.Should().BeEmpty();
            bob.IncomingInviteToGameRequests.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveInviteFriendToGame_Should_ReturnTrue_When_InvitationsExist_But_DoNotMatch()
        {
            var mismatchedRoomKey = "pair-matching-WRONGCODE";
            var requestedRoomKey = "pair-matching-J843HJ";

            _context.Users.AddRange(
                new User
                {
                    Username = "alice",
                    PasswordHash = "hash123",
                    OutcomingInviteToGameRequests = new List<ToInvitationToGame>
                    {
                        new ToInvitationToGame("bob", mismatchedRoomKey)
                    }
                },
                new User
                {
                    Username = "bob",
                    PasswordHash = "hash456",
                    IncomingInviteToGameRequests = new List<FromInvitationToGame>
                    {
                        new FromInvitationToGame("alice", requestedRoomKey)
                    }
                }
            );
            await _context.SaveChangesAsync();

            var result = await _service.RemoveInviteFriendToGame("alice", "bob", "pair-matching", "J843HJ");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");
            var bob = await _context.Users.FindAsync("bob");

            if (alice == null || bob == null)
            {
                throw new Exception("Users should exist in the database.");
            }

            alice.OutcomingInviteToGameRequests.Should().ContainSingle(i => i.ToUsername == "bob" && i.RoomKey == mismatchedRoomKey);
            bob.IncomingInviteToGameRequests.Should().ContainSingle(i => i.FromUsername == "alice" && i.RoomKey == requestedRoomKey);
        }

        [Fact]
        public async Task RemoveInviteFriendToGameExpired_Should_RemoveExpiredInvites_And_ReturnUser()
        {
            var roomKey = "pair-matching-EXPIRED";
            if (_roomService.GetRoomByKey(roomKey) is not null)
            {
                throw new Exception("Room with the given key should not exist prior to the test.");
            }

            var alice = new User
            {
                Username = "alice",
                PasswordHash = "hash123",
                IncomingInviteToGameRequests = new List<FromInvitationToGame>
                {
                    new FromInvitationToGame("bob", roomKey)
                },
                OutcomingInviteToGameRequests = new List<ToInvitationToGame>
                {
                    new ToInvitationToGame("charlie", roomKey)
                }
            };

            var bob = new User
            {
                Username = "bob",
                PasswordHash = "hash456",
                OutcomingInviteToGameRequests = new List<ToInvitationToGame>
                {
                    new ToInvitationToGame("alice", roomKey)
                }
            };

            var charlie = new User
            {
                Username = "charlie",
                PasswordHash = "hash789",
                IncomingInviteToGameRequests = new List<FromInvitationToGame>
                {
                    new FromInvitationToGame("alice", roomKey)
                }
            };

            _context.Users.AddRange(alice, bob, charlie);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.RemoveInviteFriendToGameExpired("alice", _roomService);

            // Assert
            result.Should().NotBeNull();
            result!.IncomingInviteToGameRequests.Should().BeEmpty();
            result.OutcomingInviteToGameRequests.Should().BeEmpty();

            var updatedBob = await _context.Users.FindAsync("bob");
            var updatedCharlie = await _context.Users.FindAsync("charlie");

            updatedBob!.OutcomingInviteToGameRequests.Should().BeEmpty();
            updatedCharlie!.IncomingInviteToGameRequests.Should().BeEmpty();
        }

        [Fact]
        public async Task ClearAllInvitesAsync_Should_ReturnFalse_When_UserDoesNotExist()
        {
            var result = await _service.ClearAllInvitesAsync("nonexistent");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task ClearAllInvitesAsync_Should_ClearAllInvites_And_ReturnTrue()
        {
            _context.Users.Add(new User
            {
                Username = "alice",
                PasswordHash = "hash123",
                IncomingInviteToGameRequests = new List<FromInvitationToGame>
                {
                    new FromInvitationToGame("bob", "game-room1")
                },
                OutcomingInviteToGameRequests = new List<ToInvitationToGame>
                {
                    new ToInvitationToGame("charlie", "game-room2")
                }
            });
            await _context.SaveChangesAsync();

            var result = await _service.ClearAllInvitesAsync("alice");

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");

            if (alice == null)
            {
                throw new Exception("User should exist in the database.");
            }

            alice.IncomingInviteToGameRequests.Should().BeEmpty();
            alice.OutcomingInviteToGameRequests.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateMMRAsync_Should_ReturnFalse_When_UserDoesNotExist()
        {
            var mmrUpdates = new Dictionary<string, int>
            {
                { "rockpaperscissors", 1200 }
            };

            var result = await _service.UpdateMMRAsync("nonexistent", mmrUpdates);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateMMRAsync_Should_UpdateMMRs_And_ReturnTrue()
        {
            _context.Users.Add(new User
            {
                Username = "alice",
                PasswordHash = "hash123",
                RockPaperScissorsMMR = 1000,
                FourInARowMMR = 1000,
                PairMatchingMMR = 1000
            });
            await _context.SaveChangesAsync();

            var mmrUpdates = new Dictionary<string, int>
            {
                { "rockpaperscissors", 1100 },
                { "fourinarow", 1150 },
                { "pairmatching", 1200 }
            };

            var result = await _service.UpdateMMRAsync("alice", mmrUpdates);

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");

            if (alice == null)
            {
                throw new Exception("User should exist in the database.");
            }

            alice.RockPaperScissorsMMR.Should().Be(1100);
            alice.FourInARowMMR.Should().Be(1150);
            alice.PairMatchingMMR.Should().Be(1200);
        }

        [Fact]
        public async Task UpdateMMRAsync_Should_IgnoreUnknownGameTypes_And_StillReturnTrue()
        {
            _context.Users.Add(new User
            {
                Username = "alice",
                PasswordHash = "hash123",
                RockPaperScissorsMMR = 1000
            });
            await _context.SaveChangesAsync();

            var mmrUpdates = new Dictionary<string, int>
            {
                { "rockpaperscissors", 1300 },
                { "unknownGame", 999 }
            };

            var result = await _service.UpdateMMRAsync("alice", mmrUpdates);

            result.Should().BeTrue();

            var alice = await _context.Users.FindAsync("alice");

            if (alice == null)
            {
                throw new Exception("User should exist in the database.");
            }

            alice.RockPaperScissorsMMR.Should().Be(1300);
        }

        [Fact]
        public async Task GetAllUsersAsync_Should_ReturnEmptyList_When_NoUsersExist()
        {
            var result = await _service.GetAllUsersAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllUsersAsync_Should_ReturnAllUsers_When_UsersExist()
        {
            _context.Users.AddRange(
                new User { Username = "alice", PasswordHash = "hash123" },
                new User { Username = "bob", PasswordHash = "hash456" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.GetAllUsersAsync();

            result.Should().HaveCount(2);
            result.Select(u => u.Username).Should().Contain(new[] { "alice", "bob" });
        }

        [Fact]
        public void HashPassword_Should_ReturnSHA256HexString()
        {
            var password = "mySecurePassword123";
            var expectedHash = Convert.ToHexString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password)));

            var result = _service.HashPassword(password);

            result.Should().Be(expectedHash);
        }

        [Fact]
        public void HashPassword_Should_ReturnDifferentHashes_ForDifferentPasswords()
        {
            var hash1 = _service.HashPassword("password1");
            var hash2 = _service.HashPassword("password2");

            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void GetUserByUsername_Should_ReturnNull_When_UserDoesNotExist()
        {
            var result = _service.GetUserByUsername("nonexistent");

            result.Should().BeNull();
        }

        [Fact]
        public void GetUserByUsername_Should_ReturnUser_When_UsernameExists()
        {
            _context.Users.Add(new User
            {
                Username = "alice",
                PasswordHash = "hash123"
            });
            _context.SaveChanges();

            var result = _service.GetUserByUsername("alice");

            result.Should().NotBeNull();
            result!.Username.Should().Be("alice");
        }

        [Fact]
        public async Task ApplyGameResultAsync_Should_ReturnTrue_When_IsDraw()
        {
            var result = await _service.ApplyGameResultAsync("rock-paper-scissors", null, null, isDraw: true);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task ApplyGameResultAsync_Should_ReturnFalse_When_UsernamesAreInvalid()
        {
            var result = await _service.ApplyGameResultAsync("rock-paper-scissors", "", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task ApplyGameResultAsync_Should_ReturnFalse_When_UsersDoNotExist()
        {
            var result = await _service.ApplyGameResultAsync("rock-paper-scissors", "alice", "bob");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task ApplyGameResultAsync_Should_ReturnFalse_For_UnknownGameType()
        {
            _context.Users.AddRange(
                new User { Username = "alice", PasswordHash = "hash123" },
                new User { Username = "bob", PasswordHash = "hash456" }
            );
            await _context.SaveChangesAsync();

            var result = await _service.ApplyGameResultAsync("unknown-game", "alice", "bob");

            result.Should().BeFalse();
        }

    }
}