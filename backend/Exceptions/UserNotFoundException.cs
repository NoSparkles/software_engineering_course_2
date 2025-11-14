using System;

namespace Exceptions
{
    public class UserNotFoundException : Exception
    {
        public string? Username { get; }
        public string? ErrorCode { get; }
        public string? Operation { get; }

        // Parameterless constructor (required by ExceptionManager<TException> where TException : new())
        public UserNotFoundException()
            : base("User not found.")
        {
        }

        // Constructor with message (used by CreateNew)
        public UserNotFoundException(string message)
            : base(message)
        {
        }

        // Rich constructor with context
        public UserNotFoundException(string username, string errorCode, string operation)
            : base($"User '{username}' not found during {operation}. Error code: {errorCode}")
        {
            Username = username;
            ErrorCode = errorCode;
            Operation = operation;
        }

        // Constructor with inner exception
        public UserNotFoundException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}