namespace samicpp.Http;

using System;
// public class Class1
// {
//     private SemaphoreSlim readLock = new(1, 1);
//     public async Task read()
//     {
//         await readLock.WaitAsync();
//         Console.WriteLine("lock succesful");
//         readLock.Release();
//     }
// }
public abstract class Helper
{
    public int somedata { get; set { field = value; Console.WriteLine($"property set to {value}"); } }
    public void Call()
    {
        Console.WriteLine("ivnoke call");
        SubFunc();
    }

    public abstract void SubFunc();
}