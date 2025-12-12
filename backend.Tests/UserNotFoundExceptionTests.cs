using System;
using Xunit;
using Exceptions;

namespace backend.Tests
{
    public class UserNotFoundExceptionTests
    {
        [Fact]
        public void Constructor_Default_SetsDefaultMessage()
        {
            // Arrange & Act
            var exception = new UserNotFoundException();
            
            // Assert
            Assert.Equal("User not found.", exception.Message);
            Assert.Null(exception.Username);
            Assert.Null(exception.ErrorCode);
            Assert.Null(exception.Operation);
        }
        
        [Fact]
        public void Constructor_WithMessage_SetsMessage()
        {
            // Arrange
            var message = "Custom user not found message";
            
            // Act
            var exception = new UserNotFoundException(message);
            
            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Null(exception.Username);
            Assert.Null(exception.ErrorCode);
            Assert.Null(exception.Operation);
        }
        
        [Fact]
        public void Constructor_WithContext_SetsAllProperties()
        {
            // Arrange
            var username = "testuser";
            var errorCode = "USER_404";
            var operation = "GetUserProfile";
            
            // Act
            var exception = new UserNotFoundException(username, errorCode, operation);
            
            // Assert
            // Check the actual message format (might have trailing space)
            Assert.StartsWith($"User '{username}' not found during {operation}. Error code: {errorCode}", 
                exception.Message.TrimEnd());
            Assert.Equal(username, exception.Username);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.Equal(operation, exception.Operation);
        }
        
        [Fact]
        public void Constructor_WithInnerException_SetsInnerException()
        {
            // Arrange
            var message = "User lookup failed";
            var innerException = new InvalidOperationException("Database connection failed");
            
            // Act
            var exception = new UserNotFoundException(message, innerException);
            
            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(innerException, exception.InnerException);
            Assert.Null(exception.Username);
            Assert.Null(exception.ErrorCode);
            Assert.Null(exception.Operation);
        }
        
        [Fact]
        public void Constructor_WithContext_HandlesNullValues()
        {
            // Arrange
            string username = null!;
            string errorCode = null!;
            string operation = null!;
            
            // Act
            var exception = new UserNotFoundException(username, errorCode, operation);
            
            // Assert - Trim any trailing spaces
            var message = exception.Message.TrimEnd();
            Assert.StartsWith("User '' not found during . Error code:", message);
            Assert.Null(exception.Username);
            Assert.Null(exception.ErrorCode);
            Assert.Null(exception.Operation);
        }
        
        [Fact]
        public void Constructor_WithContext_HandlesEmptyStrings()
        {
            // Arrange
            var username = "";
            var errorCode = "";
            var operation = "";
            
            // Act
            var exception = new UserNotFoundException(username, errorCode, operation);
            
            // Assert - Trim trailing spaces for comparison
            Assert.Equal("User '' not found during . Error code:", 
                exception.Message.TrimEnd());
            Assert.Equal("", exception.Username);
            Assert.Equal("", exception.ErrorCode);
            Assert.Equal("", exception.Operation);
        }
        
        [Fact]
        public void Properties_CanBeAccessedAfterThrowing()
        {
            // Arrange
            var username = "john_doe";
            var errorCode = "NOT_FOUND";
            var operation = "Authentication";
            
            UserNotFoundException? caughtException = null;
            
            // Act
            try
            {
                throw new UserNotFoundException(username, errorCode, operation);
            }
            catch (UserNotFoundException ex)
            {
                caughtException = ex;
            }
            
            // Assert
            Assert.NotNull(caughtException);
            Assert.Equal(username, caughtException.Username);
            Assert.Equal(errorCode, caughtException.ErrorCode);
            Assert.Equal(operation, caughtException.Operation);
        }
        
        [Fact]
        public void Exception_CanBeCaughtAsBaseException()
        {
            // Arrange
            var exception = new UserNotFoundException("testuser", "ERR_001", "Login");
            
            // Act & Assert
            Exception? baseException = null;
            try
            {
                throw exception;
            }
            catch (Exception ex)
            {
                baseException = ex;
            }
            
            Assert.NotNull(baseException);
            Assert.IsType<UserNotFoundException>(baseException);
        }
        
        [Fact]
        public void ToString_IncludesExceptionTypeAndMessage()
        {
            // Arrange
            var exception = new UserNotFoundException("testuser", "ERR_001", "Login");
            
            // Act
            var stringRepresentation = exception.ToString();
            
            // Assert
            Assert.Contains("UserNotFoundException", stringRepresentation);
            Assert.Contains("testuser", stringRepresentation);
            Assert.Contains("ERR_001", stringRepresentation);
            Assert.Contains("Login", stringRepresentation);
        }
        
        [Fact]
        public void Message_PropertyIsReadable()
        {
            // Arrange
            var messages = new string[]
            {
                "User not found.",
                "User 'admin' not found.",
                "User '' not found during . Error code:"
            };
            
            // Act & Assert
            foreach (var expectedMessage in messages)
            {
                var exception = new UserNotFoundException(expectedMessage);
                // Trim for comparison since the constructor might add trailing space
                Assert.Equal(expectedMessage, exception.Message.TrimEnd());
            }
        }
    }
}