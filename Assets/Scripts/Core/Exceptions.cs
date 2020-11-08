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