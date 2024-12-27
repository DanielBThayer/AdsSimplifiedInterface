// Ignore Spelling: Plc

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// Variable information for a PLC variable
    /// </summary>
    public class PlcVariableTypeInfo
    {
        #region Member Variables
        /// <summary>
        /// Name of the variable type
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Comment for the variable type
        /// </summary>
        public string Comment { get; set; } = string.Empty;
        /// <summary>
        /// Data type of the variable type
        /// </summary>
        public string DataType { get; set; } = string.Empty;
        /// <summary>
        /// Base data type of the variable type, if an enumeration
        /// </summary>
        public string BaseDataType { get; set; } = string.Empty;
        /// <summary>
        /// Size of the variable type in bytes
        /// </summary>
        public int Size { get; set; } = 0;
        /// <summary>
        /// Offset in the parent type in bytes
        /// </summary>
        public int Offset { get; set; } = 0;
        /// <summary>
        /// Size of the array if the variable type is an array
        /// </summary>
        public int ArraySize { get; set; } = 0;
        /// <summary>
        /// Enumeration value if the variable type is an enumeration value
        /// </summary>
        public object EnumValue { get; set; } = 0;
        /// <summary>
        /// Variable type is an array
        /// </summary>
        public bool IsArray { get; set; } = false;
        /// <summary>
        /// Variable type is an enumeration
        /// </summary>
        public bool IsEnum { get; set; } = false;
        /// <summary>
        /// Variable type is an enumeration value
        /// </summary>
        public bool IsEnumValue { get; set; } = false;
        /// <summary>
        /// Variable type is a string
        /// </summary>
        public bool IsString { get; set; } = false;
        /// <summary>
        /// Variable type is a boolean
        /// </summary>
        public bool IsBoolean { get; set; } = false;
        /// <summary>
        /// Variable type is an address type
        /// </summary>
        public bool IsAddressType { get; set; } = false;
        /// <summary>
        /// Variable type is a persistent variable
        /// </summary>
        public bool IsPersistent { get; set; } = false;
        /// <summary>
        /// Variable type is a read-only variable (i.e. can't be written to PLC)
        /// </summary>
        public bool IsReadOnly { get; set; } = false;
        /// <summary>
        /// Variable can be written to the PLC as a block
        /// </summary>
        public bool IsBlockWriteAllowed { get; set; } = true;
        /// <summary>
        /// Variable type information for children of this type
        /// </summary>
        public List<PlcVariableTypeInfo> Children { get; set; } = [];
        /// <summary>
        /// Attributes for the variable type
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; } = [];
        #endregion

        #region Constructor(s)
        /// <summary>
        /// Default constructor
        /// </summary>
        public PlcVariableTypeInfo()
        {
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Version to copy</param>
        public PlcVariableTypeInfo(PlcVariableTypeInfo other)
        {
            Name = other.Name;
            Comment = other.Comment;
            DataType = other.DataType;
            BaseDataType = other.BaseDataType;
            Size = other.Size;
            Offset = other.Offset;
            ArraySize = other.ArraySize;
            EnumValue = other.EnumValue;
            IsArray = other.IsArray;
            IsEnum = other.IsEnum;
            IsEnumValue = other.IsEnumValue;
            IsString = other.IsString;
            IsBoolean = other.IsBoolean;
            IsAddressType = other.IsAddressType;
            IsPersistent = other.IsPersistent;
            IsReadOnly = other.IsReadOnly;
            IsBlockWriteAllowed = other.IsBlockWriteAllowed;
            Children = other.Children.Select(child => new PlcVariableTypeInfo(child)).ToList();
            Attributes = new Dictionary<string, string>(other.Attributes);
        }
        #endregion
    }
}
