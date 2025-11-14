namespace Exceptions
{
    public class ExceptionFileLogger : IExceptionLogger
    {
        private readonly string _filePath;

        public ExceptionFileLogger(string filePath = "ExceptionLogs.txt")
        {
            _filePath = filePath;
        }
        public ExceptionFileLogger() : this("ExceptionLogs.txt")
        {
        }

        public void Log(Exception ex)
        {
            try
            {
                using var writer = new StreamWriter(_filePath, true);
                writer.WriteLine($"{DateTime.Now}: {ex.GetType().Name} - {ex.Message}");
            }
            catch
            {
                Console.WriteLine("Failed to write to log file.");
            }
        }
    }
}