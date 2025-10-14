namespace Samicpp.Tests;

using System;
using System.Threading.Tasks;
using Samicpp.Http.Debug;
using Xunit;
using Samicpp.Http.Http2;


public class Tests
{
    [Fact]
    public void test1()
    {
        Console.WriteLine("run test");
    }

    [Fact]
    public void test2()
    {
        FakeHttpSocket sock = new();

    }

    [Fact]
    public void frameParseTest()
    {
        Http2Frame frame = Http2Frame.Parse([0, 0, 14, 0, 0, 0, 1, 0, 49, 5, 0, 0, 0, 1, 5, 97, 98, 99, 1, 2, 3, 4, 5,]);
        Console.WriteLine(frame.payload);
        Console.WriteLine(frame.priority);
        Console.WriteLine(frame.padding);
        Console.Write("frame = [ ");
        foreach (byte b in frame.ToBytes()) Console.Write($"{b}, ");
        Console.WriteLine("]");
    }


    [Fact]
    [Trait("Category", "Network")]
    public async Task TcpEchoServer()
    {
        await Task.Delay(0);
    }

}
