namespace Exceptions
{
    public class ExceptionConsoleLogger : IExceptionLogger
    {
        public void Log(Exception ex)
        {
            Console.WriteLine($"[ConsoleLogger] {ex.GetType().Name}: {ex.Message}");
        }
    }
}