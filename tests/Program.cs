namespace samicpp.tests;

using System;
using samicpp.Http;
using Xunit;


class Something: Helper
{
    override public void SubFunc()
    {
        Console.WriteLine("crazy");
    }
}

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
        Something some=new();

        some.Call();
        some.somedata = 1;
        
    }

}