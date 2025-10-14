namespace Samicpp.Tests;

using System;
using System.Threading.Tasks;
using Samicpp.Http.Debug;
using Xunit;
using Samicpp.Http.Http1;


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
    [Trait("Category", "Network")]
    public async Task TcpEchoServer()
    {
        await Task.Delay(0);
    }

}
