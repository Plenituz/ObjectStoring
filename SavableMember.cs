namespace Motio.ObjectStoring
{
    /// <summary>
    /// helper class to store a name an a value
    /// </summary>
    public class SavableMember
    {
        public string name;
        public object value;

        public SavableMember(string name, object value)
        {
            this.name = name;
            this.value = value;
        }
    }
}
