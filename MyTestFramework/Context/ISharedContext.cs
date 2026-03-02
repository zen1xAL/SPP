namespace MyTestFramework.Context
{
    public interface ISharedContext<T> where T : class, new()
    {
        void SetContext(T context);
    }
}
