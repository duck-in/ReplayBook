using System;
using LiteDB;

namespace Fraxiinus.ReplayBook.Files.Models;

public class ReplayErrorInfo
{
    public ReplayErrorInfo() { }

    public ReplayErrorInfo(string filePath, string exceptionType, string exceptionString, string exceptionCallStack)
    {
        FilePath = filePath;
        ExceptionType = exceptionType;
        ExceptionString = exceptionString;
        ExceptionCallStack = exceptionCallStack;
    }
    
    [BsonId]
    public string FilePath { get; set; }

    public string ExceptionType { get; set; }

    public string ExceptionString { get; set; }

    public string ExceptionCallStack { get; set; }
}