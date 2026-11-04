namespace Invoicing.Domain;

// An application exception. Each host turns it into a safe response through its error hook.
public sealed class InvoiceNotFoundException : Exception
{
    public InvoiceNotFoundException()
        : base(InvoicingErrors.InvoiceNotFound)
    {
    }

    public InvoiceNotFoundException(string message)
        : base(message)
    {
    }

    public InvoiceNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
