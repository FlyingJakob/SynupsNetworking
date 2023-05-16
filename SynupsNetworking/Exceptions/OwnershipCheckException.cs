namespace SynupsNetworking.Exceptions
{
    public class OwnershipCheckException : System.Exception
    {
        public OwnershipCheckException() { }

        public OwnershipCheckException(string message) : base(message) { }
    }

}