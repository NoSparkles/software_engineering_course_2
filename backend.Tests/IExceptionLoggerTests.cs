using System;
using Xunit;
using Exceptions;

namespace backend.Tests
{
    public class IExceptionLoggerTests
    {
        // Test implementation of IExceptionLogger
        private class TestLogger : IExceptionLogger
        {
            public bool WasCalled { get; private set; }
            public Exception? LastException { get; private set; }
            public int CallCount { get; private set; }
            
            public void Log(Exception ex)
            {
                WasCalled = true;
                LastException = ex;
                CallCount++;
            }
        }
        
        // Another test implementation
        private class CountingLogger : IExceptionLogger
        {
            public int Count { get; private set; }
            
            public void Log(Exception ex)
            {
                Count++;
            }
        }
        
        [Fact]
        public void Interface_CanBeImplemented()
        {
            // Arrange
            IExceptionLogger logger = new TestLogger();
            var exception = new Exception("Test");
            
            // Act
            logger.Log(exception);
            
            // Assert
            var testLogger = (TestLogger)logger;
            Assert.True(testLogger.WasCalled);
            Assert.Same(exception, testLogger.LastException);
        }
        
        [Fact]
        public void DifferentImplementations_CanBeUsed()
        {
            // Arrange
            IExceptionLogger logger1 = new TestLogger();
            IExceptionLogger logger2 = new CountingLogger();
            var exception = new Exception("Test");
            
            // Act
            logger1.Log(exception);
            logger2.Log(exception);
            
            // Assert
            Assert.True(((TestLogger)logger1).WasCalled);
            Assert.Equal(1, ((CountingLogger)logger2).Count);
        }
        
        [Fact]
        public void Logger_CanBeUsedInGenericContext()
        {
            // Arrange
            var logger = new TestLogger();
            var exception = new InvalidOperationException("Generic test");
            
            // Act
            LogWithLogger(logger, exception);
            
            // Assert
            Assert.True(logger.WasCalled);
            Assert.Same(exception, logger.LastException);
        }
        
        [Fact]
        public void Logger_CanHandleMultipleCalls()
        {
            // Arrange
            var logger = new TestLogger();
            var exceptions = new Exception[]
            {
                new Exception("First"),
                new ArgumentException("Second"),
                new UserNotFoundException("Third")
            };
            
            // Act
            foreach (var ex in exceptions)
            {
                logger.Log(ex);
            }
            
            // Assert
            Assert.Equal(3, logger.CallCount);
            Assert.Same(exceptions[2], logger.LastException);
        }
        
        [Fact]
        public void RealLoggers_ImplementInterfaceCorrectly()
        {
            // Arrange
            IExceptionLogger consoleLogger = new ExceptionConsoleLogger();
            IExceptionLogger fileLogger = new ExceptionFileLogger();
            var exception = new Exception("Interface test");
            
            // Act & Assert - Should not throw
            var consoleException = Record.Exception(() => consoleLogger.Log(exception));
            var fileException = Record.Exception(() => fileLogger.Log(exception));
            
            Assert.Null(consoleException);
            Assert.Null(fileException);
        }
        
        [Fact]
        public void Logger_CanBePassedAsParameter()
        {
            // Arrange
            var logger = new TestLogger();
            var exception = new Exception("Parameter test");
            
            // Act
            ProcessException(logger, exception);
            
            // Assert
            Assert.True(logger.WasCalled);
            Assert.Same(exception, logger.LastException);
        }
        
        // Helper methods to test interface usage
        private void LogWithLogger(IExceptionLogger logger, Exception ex)
        {
            logger.Log(ex);
        }
        
        private void ProcessException(IExceptionLogger logger, Exception ex)
        {
            logger.Log(ex);
        }
    }
}