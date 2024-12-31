using AdsSimplifiedInterface.Attributes;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using TwinCAT.TypeSystem;

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// Creates data types from PLC data type information
    /// </summary>
    /// <remarks>
    /// Constructor for ADS type creator
    /// </remarks>
    /// <param name="logger">Log System</param>
    internal class AdsTypeCreator(ILogger logger)
    {
        #region Members
        /// <summary>
        /// Cache of converted types
        /// </summary>
        private readonly ConcurrentDictionary<string, Type> _cache = new();
        /// <summary>
        /// Log System
        /// </summary>
        private readonly ILogger _logger = logger;
        /// <summary>
        /// Builder for creating data types
        /// </summary>
        private readonly ModuleBuilder _moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("PLC"), AssemblyBuilderAccess.RunAndCollect).DefineDynamicModule("PLC");
        /// <summary>
        /// Constructor for adding MarshalAs(marshalType)
        /// </summary>
        private readonly ConstructorInfo _marshalType = typeof(MarshalAsAttribute).GetConstructor([typeof(UnmanagedType)]) ?? throw new InvalidOperationException("MarshalAs doesn't define a constructor with just the unmanaged type");
        /// <summary>
        /// Constructor for adding MarshalAs(marshalType)
        /// </summary>
        private readonly ConstructorInfo _readOnlyAttribute = typeof(ReadOnlyAttribute).GetConstructor([]) ?? throw new InvalidOperationException("Read Only Attribute doesn't define a constructor with just the unmanaged type");
        /// <summary>
        /// Constructor for adding MarshalAs(marshalType)
        /// </summary>
        private readonly ConstructorInfo _blockWriteNotAllowedAttribute = typeof(BlockWriteNotAllowedAttribute).GetConstructor([]) ?? throw new InvalidOperationException("Block Write Not Allowed Attribute doesn't define a constructor with just the unmanaged type");
        /// <summary>
        /// Constant size field of the MarshalAs attribute
        /// </summary>
        private readonly FieldInfo _marshalConstSize = typeof(MarshalAsAttribute).GetField("SizeConst") ?? throw new InvalidOperationException("MarshalAs has not field SizeConst");
        /// <summary>
        /// Array sub type field of the MarshalAs attribute
        /// </summary>
        private readonly FieldInfo _marshalSubType = typeof(MarshalAsAttribute).GetField("ArraySubType") ?? throw new InvalidOperationException("MarshalAs has not field ConstSize");
        #endregion

        #region Cache Management
        /// <summary>
        /// Removes all types from the cache
        /// </summary>
        public void ResetCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Adds a type to the cache
        /// </summary>
        /// <param name="typeName">Name of the type</param>
        /// <param name="type">Type to use</param>
        public void RegisterType(string typeName, Type type)
        {
            _cache.TryAdd(typeName, type);
        }
        #endregion

        #region Data Type Creation
        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        public Type CreateType(IDataType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            // Search the defined types
            if (Type.GetType(dataType.FullName) is Type systemType)
            {
                _cache[dataType.FullName] = systemType;
                return systemType;
            }

            _logger.LogDebug($"Starting {dataType.FullName} creation");

            // Create the type
            Type type = dataType switch
            {
                IStructType => CreateType((IStructType)dataType),
                IArrayType => CreateType((IArrayType)dataType),
                IPointerType => CreateType((IPointerType)dataType),
                IEnumType => CreateType((IEnumType)dataType),
                IReferenceType => CreateType((IReferenceType)dataType),
                IAliasType => CreateType((IAliasType)dataType),
                IInterfaceType => CreateType((IInterfaceType)dataType),
                IPrimitiveType => CreateType((IPrimitiveType)dataType),
                IStringType => CreateType((IStringType)dataType),
                IUnionType => CreateType((IUnionType)dataType),
                ISubRangeType => CreateType((ISubRangeType)dataType),
                _ => throw new InvalidOperationException($"{dataType.FullName} is not a known type")
            };

            _logger.LogDebug($"Finished {dataType.FullName} creation");

            // Cache the results
            _cache[dataType.FullName] = type;

            // Return the results
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IStructType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} structure creation");

            // Create the structure
            TypeBuilder builder = _moduleBuilder.DefineType(dataType.Name, TypeAttributes.SequentialLayout);

            // Add the members
            bool BlockWriteNotAllowed = false;
            int offset = 0;
            int alignment;
            int alignmentNumber = 1;
            foreach (IMember member in dataType.Members)
            {
                _logger.LogDebug($"Starting {member.InstancePath} addition");

                // Skip if member is typeless
                if (member.DataType == null)
                {
                    _logger.LogDebug($"{member.InstancePath} is missing data type");
                    continue;
                }

                // Check for alignment
                alignment = member.ByteOffset - offset;
                if (alignment > 0)
                {
                    _logger.LogDebug($"Adding a {alignment} alignment before {member.InstancePath}");
                    CreateAlignment(alignment, alignmentNumber, ref builder);
                    alignmentNumber++;
                    offset += alignment;
                }

                // Create the field
                FieldBuilder field = builder.DefineField(member.InstanceName, CreateType(member.DataType), FieldAttributes.Public);
                SetAttributes(member.DataType, ref field);

                // Check if the field is read only
                if (member.Attributes.Contains("ReadOnly"))
                {
                    field.SetCustomAttribute(new CustomAttributeBuilder(_readOnlyAttribute, []));
                    BlockWriteNotAllowed = true;
                }
                else if (ReadOnlyVariable(member.DataType) || member.Attributes.Contains("NotBlockWritable") || field.FieldType.IsDefined(typeof(BlockWriteNotAllowedAttribute)))
                {
                    BlockWriteNotAllowed = true;
                }

                // Update offset
                offset += member.ByteSize;

                _logger.LogDebug($"{member.InstancePath} added with next offset at {offset}");
            }

            // Check for alignment
            alignment = dataType.ByteSize - offset;
            if (alignment > 0)
            {
                _logger.LogDebug($"Adding a {alignment} alignment at the end of {dataType.FullName}");
                CreateAlignment(alignment, alignmentNumber, ref builder);
            }

            // Check if the structure is block writable or not
            if (BlockWriteNotAllowed || dataType.Attributes.Contains("NotBlockWritable"))
            {
                builder.SetCustomAttribute(new CustomAttributeBuilder(_blockWriteNotAllowedAttribute, []));
            }

            // Check if the structure is read only
            if (ReadOnlyVariable(dataType))
            {
                builder.SetCustomAttribute(new CustomAttributeBuilder(_readOnlyAttribute, []));
            }

            // Cache the type and return
            Type type = builder.CreateType();
            _cache[dataType.FullName] = type;

            _logger.LogDebug($"{dataType.FullName} created successfully");
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IArrayType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} array creation");

            // Create the element type
            IDataType elementType = dataType.ElementType ?? throw new InvalidOperationException($"{dataType.FullName} does not have an element data type");

            // Create the array type, cache, and return
            Type type = CreateType(elementType).MakeArrayType();
            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IPointerType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} pointer creation");

            if (dataType.ByteSize == 4)
            {
                _logger.LogDebug($"{dataType.FullName} is a 32-bit reference");
                _cache[dataType.FullName] = typeof(UInt32);
                return typeof(UInt32);
            }
            else
            {
                _logger.LogDebug($"{dataType.FullName} is a 64-bit reference");
                _cache[dataType.FullName] = typeof(UInt64);
                return typeof(UInt64);
            }
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IEnumType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} enum creation");

            // Build the enumeration
            EnumBuilder builder = _moduleBuilder.DefineEnum(dataType.Name, TypeAttributes.Public, CreateType(dataType.BaseType ?? throw new InvalidOperationException($"{dataType.FullName} does not have an underlying type")));

            // Add the values
            foreach (IEnumValue enumValue in dataType.EnumValues)
            {
                _logger.LogDebug($"Starting {enumValue.Name} addition");

                // Create the value
                switch (dataType.BaseType.Name.ToUpperInvariant())
                {
                    case "BYTE":
                    case "USINT":
                        builder.DefineLiteral(enumValue.Name, enumValue.RawValue[0]);
                        break;
                    case "SBYTE":
                    case "SINT":
                        builder.DefineLiteral(enumValue.Name, (sbyte)enumValue.RawValue[0]);
                        break;
                    case "INT":
                        builder.DefineLiteral(enumValue.Name, BitConverter.ToInt16(enumValue.RawValue, 0));
                        break;
                    case "UINT":
                        builder.DefineLiteral(enumValue.Name, BitConverter.ToUInt16(enumValue.RawValue, 0));
                        break;
                    case "DINT":
                        builder.DefineLiteral(enumValue.Name, BitConverter.ToInt32(enumValue.RawValue, 0));
                        break;
                    case "UDINT":
                        builder.DefineLiteral(enumValue.Name, BitConverter.ToUInt32(enumValue.RawValue, 0));
                        break;
                    case "LINT":
                        builder.DefineLiteral(enumValue.Name, BitConverter.ToInt64(enumValue.RawValue, 0));
                        break;
                    case "ULINT":
                        builder.DefineLiteral(enumValue.Name, BitConverter.ToUInt64(enumValue.RawValue, 0));
                        break;
                    default:
                        throw new InvalidOperationException($"Enumeration {dataType.FullName} has a value beyond allowed limits of TwinCAT");
                }

                _logger.LogDebug($"{enumValue.Name} added to the enumeration");
            }

            // Cache the type and return
            Type type = builder.CreateType();
            _cache[dataType.FullName] = type;

            _logger.LogDebug($"{dataType.FullName} created successfully");
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IReferenceType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} reference creation");

            if (dataType.ByteSize == 4)
            {
                _logger.LogDebug($"{dataType.FullName} is a 32-bit reference");
                _cache[dataType.FullName] = typeof(UInt32);
                return typeof(UInt32);
            }
            else
            {
                _logger.LogDebug($"{dataType.FullName} is a 64-bit reference");
                _cache[dataType.FullName] = typeof(UInt64);
                return typeof(UInt64);
            }
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IAliasType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} alias creation");

            // Create the real type, cache and send
            Type type = CreateType(dataType.BaseType ?? throw new InvalidOperationException($"{dataType.FullName} does not have a resolvable type"));
            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IInterfaceType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} interface creation");

            if (dataType.ByteSize == 4)
            {
                _logger.LogDebug($"{dataType.FullName} is a 32-bit interface");
                _cache[dataType.FullName] = typeof(UInt32);
                return typeof(UInt32);
            }
            else
            {
                _logger.LogDebug($"{dataType.FullName} is a 64-bit interface");
                _cache[dataType.FullName] = typeof(UInt64);
                return typeof(UInt64);
            }
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IPrimitiveType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} primitive creation");

            // Manual conversion
            Type type = dataType.Name.ToUpperInvariant() switch
            {
                "BOOL" => typeof(bool),
                "BYTE" or "USINT" => typeof(byte),
                "SBYTE" or "SINT" => typeof(sbyte),
                "UINT" or "WORD" => typeof(UInt16),
                "INT" => typeof(Int16),
                "UDINT" or "TIME" or "DATE" or "DWORD" or "DATE_AND_TIME" or "DT" => typeof(UInt32),
                "DINT" => typeof(Int32),
                "ULINT" or "LTIME" or "LDATE" or "LWORD" or "UXINT" => typeof(UInt64),
                "LINT" or "XINT" => typeof(Int64),
                "REAL" => typeof(Single),
                "LREAL" => typeof(Double),
                _ => throw new NotImplementedException()
            };

            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IStringType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} string creation");

            return typeof(string);
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(IUnionType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} union creation");

            // Build the union
            TypeBuilder builder = _moduleBuilder.DefineType(dataType.Name, TypeAttributes.ExplicitLayout);

            // Fetch the members and add them to the union
            foreach (IField member in dataType.Fields)
            {
                _logger.LogDebug($"Starting {member.InstancePath} addition");

                // Skip if member is typeless
                if (member.DataType == null)
                {
                    _logger.LogDebug($"{member.InstancePath} is missing data type");
                    continue;
                }

                // Protect against invalid types
                if (member.DataType is IStringType)
                {
                    throw new NotImplementedException($"{member.InstancePath} is not able to be handled right now because it is a string and C# doesn't like unions with strings");
                }
                if (ReadOnlyVariable(member.DataType))
                {
                    throw new InvalidOperationException($"{member.InstancePath} is not valid because the member's type is read only");
                }

                // Create the field and add attributes
                FieldBuilder field = builder.DefineField(member.InstanceName, CreateType(member.DataType), FieldAttributes.Public);
                SetAttributes(member.DataType, ref field);
                field.SetOffset(0);

                _logger.LogDebug($"{member.InstancePath} added to the union");
            }

            // Cache the type and return
            Type type = builder.CreateType();
            _cache[dataType.FullName] = type;

            _logger.LogDebug($"{dataType.FullName} created successfully");
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private Type CreateType(ISubRangeType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out Type? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} subrange creation");

            // Create the real type, cache and send
            Type type = CreateType(dataType.BaseType ?? throw new InvalidOperationException($"{dataType.FullName} does not have a resolvable type"));
            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Sets the attributes for properly Marshalling a field
        /// </summary>
        /// <param name="dataType">Data type information of the field</param>
        /// <param name="fieldBuilder">Field to marshal</param>
        private void SetAttributes(IDataType dataType, ref FieldBuilder fieldBuilder)
        {
            // Handle Marshalling for the field
            if (dataType is IArrayType value)
            {
                if (value.ElementType!.Name.Equals("bool", StringComparison.OrdinalIgnoreCase))
                {
                    fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_marshalType, [UnmanagedType.ByValArray], [_marshalConstSize, _marshalSubType], [dataType.ByteSize / value.ElementType!.ByteSize, UnmanagedType.I1]));
                }
                else if (value.ElementType is IStringType)
                {
                    // Blocks group write operations because there is no way to do this for string arrays
                    fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_blockWriteNotAllowedAttribute, []));
                }
                else
                {
                    fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_marshalType, [UnmanagedType.ByValArray], [_marshalConstSize], [dataType.ByteSize / value.ElementType!.ByteSize]));
                }
            }
            else if (dataType is IPrimitiveType)
            {
                if (dataType.Name.Equals("bool", StringComparison.OrdinalIgnoreCase))
                {
                    fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_marshalType, [UnmanagedType.I1]));
                }
            }
            else if (dataType is IStringType)
            {
                fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_marshalType, [UnmanagedType.ByValTStr], [_marshalConstSize], [dataType.ByteSize]));
            }

            // Handle read-only fields
            if (ReadOnlyVariable(dataType))
            {
                fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_readOnlyAttribute, []));
            }
        }

        /// <summary>
        /// Creates an alignment entry into the data type
        /// </summary>
        /// <param name="Size">Size of the alignment</param>
        /// <param name="AlignmentNumber">Number of the alignment</param>
        /// <param name="typeBuilder">Type builder to add the alignment to</param>
        private void CreateAlignment(int Size, int AlignmentNumber, ref TypeBuilder typeBuilder)
        {
            if (Size == 0)
            {
                return;
            }
            else if (Size == 1)
            {
                typeBuilder.DefineField("Alignment" + AlignmentNumber.ToString(), typeof(byte), FieldAttributes.Private);
                return;
            }
            else
            {
                FieldBuilder fieldBuilder = typeBuilder.DefineField("Alignment" + AlignmentNumber.ToString(), typeof(byte[]), FieldAttributes.Private);
                fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(_marshalType, [UnmanagedType.ByValArray], [_marshalConstSize], [Size]));
            }
        }

        /// <summary>
        /// Determines if a variable is read-only
        /// </summary>
        /// <param name="dataType">Data type information</param>
        /// <returns>True if the variable is a read only variable</returns>
        public static bool ReadOnlyVariable(IDataType? dataType)
        {
            if (dataType == null)
            {
                return false;
            }

            return dataType.Attributes.Contains("ReadOnly") || (dataType is IInterfaceType && dataType is not IStructType) || dataType is IPointerType || dataType is IReferenceType;
        }
        #endregion
    }
}
