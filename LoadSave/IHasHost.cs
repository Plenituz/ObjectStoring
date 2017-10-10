namespace ObjectStoring
{
    public interface IHasHost
    {
        object GetHost();
        void SetHost(object host);
    }
}
