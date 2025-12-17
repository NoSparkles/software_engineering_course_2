using System;
using System.IO;
using Xunit;
using Exceptions;

namespace backend.Tests
{
    public class ExceptionFileLoggerTests : IDisposable
    {
        private readonly string _testFilePath;
        private readonly string _readOnlyFilePath;
        
        public ExceptionFileLoggerTests()
        {
            _testFilePath = Path.GetTempFileName();
            _readOnlyFilePath = Path.GetTempFileName();
        }
        
        public void Dispose()
        {
            // Clean up test files
            CleanupFile(_testFilePath);
            CleanupFile(_readOnlyFilePath);
        }
        
        private void CleanupFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        [Fact]
        public void Constructor_Default_CreatesLogger()
        {
            // Arrange & Act
            var logger = new ExceptionFileLogger();
            
            // Assert
            Assert.NotNull(logger);
            
            // Test it can log without throwing
            var exception = new Exception("Test");
            var exceptionThrown = Record.Exception(() => logger.Log(exception));
            Assert.Null(exceptionThrown);
        }
        
        [Fact]
        public void Constructor_WithCustomFilePath_CreatesLogger()
        {
            // Arrange & Act
            var logger = new ExceptionFileLogger(_testFilePath);
            
            // Assert
            Assert.NotNull(logger);
        }
        
        [Fact]
        public void Log_WritesExceptionToFile()
        {
            // Arrange
            var logger = new ExceptionFileLogger(_testFilePath);
            var exception = new InvalidOperationException("File logging test");
            
            // Act
            logger.Log(exception);
            
            // Assert
            Assert.True(File.Exists(_testFilePath));
            var logContent = File.ReadAllText(_testFilePath);
            Assert.Contains("File logging test", logContent);
            Assert.Contains("InvalidOperationException", logContent);
        }
        
        [Fact]
        public void Log_AppendsToExistingFile()
        {
            // Arrange
            var logger = new ExceptionFileLogger(_testFilePath);
            var exception1 = new Exception("First exception");
            var exception2 = new Exception("Second exception");
            
            // Act
            logger.Log(exception1);
            logger.Log(exception2);
            
            // Assert
            var logContent = File.ReadAllText(_testFilePath);
            var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(lines.Length >= 2);
            Assert.Contains("First exception", lines[0]);
            Assert.Contains("Second exception", lines[1]);
        }
        
        [Fact]
        public void Log_HandlesFileWriteErrors_Gracefully()
        {
            // Arrange
            File.WriteAllText(_readOnlyFilePath, "Initial content");
            File.SetAttributes(_readOnlyFilePath, FileAttributes.ReadOnly);
            
            var logger = new ExceptionFileLogger(_readOnlyFilePath);
            var exception = new Exception("Test that should fail to write");
            
            // Act
            var exceptionThrown = Record.Exception(() => logger.Log(exception));
            
            // Assert - Should not throw (catches and writes to console)
            Assert.Null(exceptionThrown);
        }
        
        [Fact]
        public void Log_HandlesNullException_WithoutThrowing()
        {
            // Arrange
            var logger = new ExceptionFileLogger(_testFilePath);
            
            // Act
            var exceptionThrown = Record.Exception(() => logger.Log(null!));
            
            // Assert - Should not throw (caught by try-catch in Log method)
            Assert.Null(exceptionThrown);
        }
        
        [Fact]
        public void Log_WritesCompleteLogLine()
        {
            // Arrange
            var logger = new ExceptionFileLogger(_testFilePath);
            var exception = new ArgumentException("Test message", "paramName");
            
            // Act
            logger.Log(exception);
            
            // Assert
            var logContent = File.ReadAllText(_testFilePath);
            Assert.Contains("ArgumentException", logContent);
            Assert.Contains("Test message", logContent);
        }
        
        [Fact]
        public void Log_WithNullException_DoesNotCreateFileEntry()
        {
            // Arrange
            var logger = new ExceptionFileLogger(_testFilePath);
            
            // Act
            logger.Log(null!);
            
            // Assert - File might exist but should be empty or contain only timestamp
            if (File.Exists(_testFilePath))
            {
                var logContent = File.ReadAllText(_testFilePath);
                // When ex is null, ex.GetType() throws NullReferenceException,
                // which is caught, so nothing should be written to file
                Assert.True(string.IsNullOrWhiteSpace(logContent) || 
                           !logContent.Contains(" - "));
            }
        }
    }
}