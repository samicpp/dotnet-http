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
    public void frameTest()
    {
        Http2Frame frame = Http2Frame.Parse([0, 0, 1, 0, 0, 0, 1, 0, 1, 97,]);
    }


    [Fact]
    [Trait("Category", "Network")]
    public async Task TcpEchoServer()
    {
        await Task.Delay(0);
    }

}
