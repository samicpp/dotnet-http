namespace samicpp.Http;

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
    public void Call()
    {
        System.Console.WriteLine("ivnoke call");
    }
}