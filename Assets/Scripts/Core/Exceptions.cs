using System;

public class SignalingException : Exception
{
    public SignalingException(string message) : base(message)
    {
    }

    public SignalingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class ConnectionException : Exception
{
    public ConnectionException(string message) : base(message)
    {
    }

    public ConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}