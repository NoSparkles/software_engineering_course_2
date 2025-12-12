using System;
using Xunit;
using Exceptions;

namespace backend.Tests
{
    public class ExceptionConsoleLoggerTests
    {
        [Fact]
        public void Log_WritesExceptionToConsole_WithoutThrowing()
        {
            // Arrange
            var logger = new ExceptionConsoleLogger();
            var exception = new InvalidOperationException("Test exception message");
            
            // Act
            var exceptionThrown = Record.Exception(() => logger.Log(exception));
            
            // Assert
            Assert.Null(exceptionThrown);
        }
        
        [Fact]
        public void Log_ThrowsNullReferenceException_WhenExceptionIsNull()
        {
            // Arrange
            var logger = new ExceptionConsoleLogger();
            
            // Act & Assert
            Assert.Throws<NullReferenceException>(() => logger.Log(null!));
        }
        
        [Fact]
        public void Log_HandlesDifferentExceptionTypes()
        {
            // Arrange
            var logger = new ExceptionConsoleLogger();
            var exceptions = new Exception[]
            {
                new ArgumentException("Invalid argument"),
                new InvalidOperationException("Invalid operation"),
                new UserNotFoundException("User not found"),
                new Exception("Generic exception")
            };
            
            // Act & Assert
            foreach (var ex in exceptions)
            {
                var exceptionThrown = Record.Exception(() => logger.Log(ex));
                Assert.Null(exceptionThrown);
            }
        }
    }
}