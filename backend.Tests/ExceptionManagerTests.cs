using System;
using Xunit;
using Exceptions;

namespace backend.Tests
{
    public class ExceptionManagerTests
    {
        private class TestException : Exception
        {
            public TestException() : base("Default message") { }
            public TestException(string message) : base(message) { }
        }
        
        [Fact]
        public void CreateNew_CreatesExceptionWithMessage()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            var message = "Custom error message";
            
            // Act
            var exception = manager.CreateNew(message);
            
            // Assert
            Assert.NotNull(exception);
            Assert.IsType<TestException>(exception);
            Assert.Equal(message, exception.Message);
        }
        
        [Fact]
        public void CreateNew_CreatesUserNotFoundException()
        {
            // Arrange
            var manager = new ExceptionManager<UserNotFoundException>();
            var message = "User not found in database";
            
            // Act
            var exception = manager.CreateNew(message);
            
            // Assert
            Assert.NotNull(exception);
            Assert.IsType<UserNotFoundException>(exception);
            Assert.Equal(message, exception.Message);
        }
        
        [Fact]
        public void CreateNew_CreatesArgumentException()
        {
            // Arrange
            var manager = new ExceptionManager<ArgumentException>();
            var message = "Argument is invalid";
            
            // Act
            var exception = manager.CreateNew(message);
            
            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
            Assert.Equal(message, exception.Message);
        }
        
        [Fact]
        public void CreateNew_CreatesInvalidOperationException()
        {
            // Arrange
            var manager = new ExceptionManager<InvalidOperationException>();
            var message = "Invalid operation occurred";
            
            // Act
            var exception = manager.CreateNew(message);
            
            // Assert
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal(message, exception.Message);
        }
        
        [Fact]
        public void Log_CallsConsoleLoggerWithoutThrowing()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            var exception = new TestException("Test exception");
            
            // Act
            var exceptionThrown = Record.Exception(() => 
                manager.Log<ExceptionConsoleLogger>(exception));
            
            // Assert
            Assert.Null(exceptionThrown);
        }
        
        [Fact]
        public void Log_CallsFileLoggerWithoutThrowing()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            var exception = new TestException("File logger test");
            
            // Act
            var exceptionThrown = Record.Exception(() => 
                manager.Log<ExceptionFileLogger>(exception));
            
            // Assert
            Assert.Null(exceptionThrown);
        }
        
        [Fact]
        public void LogAny_CallsLoggerWithAnyExceptionType()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            
            // Act & Assert - Should handle different exception types
            var exception1 = Record.Exception(() => 
                manager.LogAny<ExceptionConsoleLogger>(new InvalidOperationException()));
            var exception2 = Record.Exception(() => 
                manager.LogAny<ExceptionConsoleLogger>(new ArgumentException()));
            var exception3 = Record.Exception(() => 
                manager.LogAny<ExceptionConsoleLogger>(new UserNotFoundException()));
            
            // None should throw
            Assert.Null(exception1);
            Assert.Null(exception2);
            Assert.Null(exception3);
        }
        
        [Fact]
        public void Manager_CanLogSameExceptionMultipleTimes()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            var exception = new TestException("Multiple log test");
            
            // Act
            var results = new bool[3];
            for (int i = 0; i < 3; i++)
            {
                var exceptionThrown = Record.Exception(() => 
                    manager.Log<ExceptionConsoleLogger>(exception));
                results[i] = exceptionThrown == null;
            }
            
            // Assert
            Assert.All(results, result => Assert.True(result));
        }
        
        [Fact]
        public void CreateNew_WithEmptyMessage_CreatesException()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            
            // Act
            var exception = manager.CreateNew("");
            
            // Assert
            Assert.NotNull(exception);
            Assert.Equal("", exception.Message);
        }
        
        [Fact]
        public void Log_WithNullException_ThrowsNullReferenceException()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            
            // Act & Assert
            Assert.Throws<NullReferenceException>(() => 
                manager.Log<ExceptionConsoleLogger>(null!));
        }
        
        [Fact]
        public void LogAny_WithNullException_ThrowsNullReferenceException()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            
            // Act & Assert
            Assert.Throws<NullReferenceException>(() => 
                manager.LogAny<ExceptionConsoleLogger>(null!));
        }
        
        [Fact]
        public void CanUseManagerWithDifferentLoggerTypes()
        {
            // Arrange
            var manager = new ExceptionManager<TestException>();
            var exception = new TestException("Multiple logger test");
            
            // Act & Assert - Test with different logger types
            var consoleResult = Record.Exception(() => 
                manager.Log<ExceptionConsoleLogger>(exception));
            var fileResult = Record.Exception(() => 
                manager.Log<ExceptionFileLogger>(exception));
            
            Assert.Null(consoleResult);
            Assert.Null(fileResult);
        }
        
        [Fact]
        public void CreateNew_WorksWithDifferentGenericTypes()
        {
            // Test with various exception types that have string constructors
            TestCreateNew<ArgumentException>("Invalid argument");
            TestCreateNew<InvalidOperationException>("Invalid operation");
            TestCreateNew<ApplicationException>("Application error");
            TestCreateNew<NotImplementedException>("Not implemented");
        }
        
        private void TestCreateNew<T>(string message) where T : Exception, new()
        {
            // Arrange
            var manager = new ExceptionManager<T>();
            
            // Act
            var exception = manager.CreateNew(message);
            
            // Assert
            Assert.NotNull(exception);
            Assert.IsType<T>(exception);
            Assert.Equal(message, exception.Message);
        }
    }
}