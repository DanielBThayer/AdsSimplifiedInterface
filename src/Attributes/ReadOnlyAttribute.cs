namespace AdsSimplifiedInterface.Attributes
{
    /// <summary>
    /// Marks the PLC variable as read-only. (i.e. the AdsSimplifiedInterface will not write to the PLC variable)
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Field)]
    public class ReadOnlyAttribute : Attribute
    {
    }
}
