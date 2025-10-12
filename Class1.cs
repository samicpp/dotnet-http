namespace samicpp.http;

public class Class1
{
    private SemaphoreSlim readLock = new(1, 1);
    public async Task read()
    {
        await readLock.WaitAsync();
        Console.WriteLine("lock succesful");
        readLock.Release();
    }
}
