using Models;
using Models.InMemoryModels;
using Xunit;

namespace backend.Tests
{
    public class RoomUserTests
    {
        [Fact]
        public void Constructor_Default_ShouldInitializeProperties()
        {
            // Arrange & Act
            var roomUser = new RoomUser();
            
            // Assert
            Assert.Null(roomUser.PlayerId);
            Assert.Null(roomUser.Username);
            Assert.Null(roomUser.User);
            Assert.False(roomUser.IsPlayer);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdIsPlayerUser_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var playerId = "player-123";
            var isPlayer = true;
            var user = new User 
            { 
                Username = "testUser", 
                PasswordHash = "hashed123" 
            };
            
            // Act
            var roomUser = new RoomUser(playerId, isPlayer, user);
            
            // Assert
            Assert.Equal(playerId, roomUser.PlayerId);
            Assert.Equal(isPlayer, roomUser.IsPlayer);
            Assert.Same(user, roomUser.User);
            Assert.Equal(user.Username, roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdIsPlayerUser_WhenIsPlayerIsFalse_ShouldSetCorrectly()
        {
            // Arrange
            var playerId = "spectator-456";
            var isPlayer = false;
            var user = new User 
            { 
                Username = "spectatorUser", 
                PasswordHash = "hashed456" 
            };
            
            // Act
            var roomUser = new RoomUser(playerId, isPlayer, user);
            
            // Assert
            Assert.Equal(playerId, roomUser.PlayerId);
            Assert.False(roomUser.IsPlayer);
            Assert.Same(user, roomUser.User);
            Assert.Equal(user.Username, roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdIsPlayerUser_WhenUserIsNull_ShouldHandleNull()
        {
            // Arrange
            var playerId = "player-789";
            var isPlayer = true;
            
            // Act
            var roomUser = new RoomUser(playerId, isPlayer, null);
            
            // Assert
            Assert.Equal(playerId, roomUser.PlayerId);
            Assert.Equal(isPlayer, roomUser.IsPlayer);
            Assert.Null(roomUser.User);
            Assert.Null(roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdUser_ShouldSetPropertiesWithIsPlayerTrue()
        {
            // Arrange
            var playerId = "player-999";
            var user = new User 
            { 
                Username = "autoPlayer", 
                PasswordHash = "hashed999" 
            };
            
            // Act
            var roomUser = new RoomUser(playerId, user);
            
            // Assert
            Assert.Equal(playerId, roomUser.PlayerId);
            Assert.True(roomUser.IsPlayer);
            Assert.Same(user, roomUser.User);
            Assert.Equal(user.Username, roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdUser_WhenUserIsNull_ShouldHandleNull()
        {
            // Arrange
            var playerId = "player-null";
            
            // Act
            var roomUser = new RoomUser(playerId, null);
            
            // Assert
            Assert.Equal(playerId, roomUser.PlayerId);
            Assert.True(roomUser.IsPlayer);
            Assert.Null(roomUser.User);
            Assert.Null(roomUser.Username);
        }
    
        [Fact]
        public void Properties_CanBeSetIndividually()
        {
            // Arrange
            var roomUser = new RoomUser();
            var playerId = "custom-id";
            var username = "custom-username";
            var user = new User 
            { 
                Username = "realUser", 
                PasswordHash = "hashed111" 
            };
            var isPlayer = false;
            
            // Act
            roomUser.PlayerId = playerId;
            roomUser.Username = username;
            roomUser.User = user;
            roomUser.IsPlayer = isPlayer;
            
            // Assert
            Assert.Equal(playerId, roomUser.PlayerId);
            Assert.Equal(username, roomUser.Username);
            Assert.Same(user, roomUser.User);
            Assert.Equal(isPlayer, roomUser.IsPlayer);
        }
    
        [Fact]
        public void Username_WhenUserPropertyIsSet_ShouldNotAutomaticallyUpdate()
        {
            // Test for property independence
            // Arrange
            var roomUser = new RoomUser();
            var initialUsername = "initial";
            roomUser.Username = initialUsername;
            
            var newUser = new User 
            { 
                Username = "newUser", 
                PasswordHash = "hashed222" 
            };
            
            // Act
            roomUser.User = newUser;
            
            // Assert
            Assert.Equal(initialUsername, roomUser.Username);
            Assert.Same(newUser, roomUser.User);
            Assert.NotEqual(newUser.Username, roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdUser_ShouldCopyUsernameFromUser()
        {
            // Arrange
            var playerId = "player-copy";
            var user = new User 
            { 
                Username = "copiedUser", 
                PasswordHash = "hashed333" 
            };
            
            // Act
            var roomUser = new RoomUser(playerId, user);
            
            // Assert
            Assert.Equal(user.Username, roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdIsPlayerUser_ShouldCopyUsernameFromUser()
        {
            // Arrange
            var playerId = "player-copy2";
            var user = new User 
            { 
                Username = "copiedUser2", 
                PasswordHash = "hashed444" 
            };
            
            // Act
            var roomUser = new RoomUser(playerId, false, user);
            
            // Assert
            Assert.Equal(user.Username, roomUser.Username);
        }
    
        [Fact]
        public void PlayerId_And_Username_CanBeDifferent()
        {
            // Test that PlayerId and Username are independent properties
            // Arrange
            var roomUser = new RoomUser
            {
                PlayerId = "player-123",
                Username = "differentUsername"
            };
            
            // Act & Assert
            Assert.Equal("player-123", roomUser.PlayerId);
            Assert.Equal("differentUsername", roomUser.Username);
            Assert.NotEqual(roomUser.PlayerId, roomUser.Username);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdUser_UsesUserPlayerIdForUsername()
        {
            // Since User.PlayerId returns Username, verify the relationship
            // Arrange
            var user = new User 
            { 
                Username = "test", 
                PasswordHash = "hash" 
            };
            
            // Act
            var roomUser = new RoomUser("some-id", user);
            
            // Assert
            Assert.Equal(user.Username, roomUser.Username);
            Assert.Equal(user.PlayerId, roomUser.Username);
        }
    
        [Fact]
        public void IsPlayer_DefaultValueInDefaultConstructor_IsFalse()
        {
            // Explicit test for default value
            // Arrange & Act
            var roomUser = new RoomUser();
            
            // Assert
            Assert.False(roomUser.IsPlayer);
        }
    
        [Fact]
        public void Constructor_WithPlayerIdIsPlayerUser_WhenUserUsernameIsNull_ShouldHandleNull()
        {
            // Edge case: User with null Username
            // Arrange
            var user = new User 
            { 
                Username = null!,
                PasswordHash = "hash" 
            };
            
            // Act
            var roomUser = new RoomUser("test-id", true, user);
            
            // Assert
            Assert.Null(roomUser.Username);
            Assert.Same(user, roomUser.User);
        }

        // Additional focused tests for likely missed conditions:
        
        [Fact]
        public void IsPlayer_CanBeChangedAfterConstruction()
        {
            // Arrange
            var roomUser = new RoomUser("player-id", true, new User { Username = "test", PasswordHash = "hash" });
            
            // Act
            roomUser.IsPlayer = false;
            
            // Assert
            Assert.False(roomUser.IsPlayer);
        }
        
        [Fact]
        public void Username_CanBeSetToNull()
        {
            // Arrange
            var roomUser = new RoomUser();
            
            // Act
            roomUser.Username = null;
            
            // Assert
            Assert.Null(roomUser.Username);
        }
        
        [Fact]
        public void PlayerId_CanBeSetToNull()
        {
            // Arrange
            var roomUser = new RoomUser("original-id", true, new User { Username = "test", PasswordHash = "hash" });
            
            // Act
            roomUser.PlayerId = null;
            
            // Assert
            Assert.Null(roomUser.PlayerId);
        }
        
        [Fact]
        public void User_CanBeSetToNullAfterConstruction()
        {
            // Arrange
            var user = new User { Username = "test", PasswordHash = "hash" };
            var roomUser = new RoomUser("player-id", true, user);
            
            // Act
            roomUser.User = null;
            
            // Assert
            Assert.Null(roomUser.User);
        }
    }
}