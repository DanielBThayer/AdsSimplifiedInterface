using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TwinCAT.TypeSystem;

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// Creates data types information from PLC data type information
    /// </summary>
    /// <remarks>
    /// Constructor for ADS type creator
    /// </remarks>
    /// <param name="logger">Log System</param>
    internal class AdsTypeInfoCreator(ILogger logger)
    {
        #region Members
        /// <summary>
        /// Cache of converted types
        /// </summary>
        private readonly ConcurrentDictionary<string, PlcVariableTypeInfo> _cache = new();
        /// <summary>
        /// Log System
        /// </summary>
        private readonly ILogger _logger = logger;
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
        public void RegisterType(string typeName, PlcVariableTypeInfo type)
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
        public PlcVariableTypeInfo CreateType(IDataType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} creation");

            // Create the type
            PlcVariableTypeInfo type = dataType switch
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
        private PlcVariableTypeInfo CreateType(IStructType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} structure creation");

            PlcVariableTypeInfo plcVariableTypeInfo = new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                Offset = 0,
                DataType = dataType.Name,
                IsBlockWriteAllowed = !(dataType.Category == DataTypeCategory.FunctionBlock || dataType.Category == DataTypeCategory.Program),
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };

            foreach (IMember member in dataType.Members)
            {
                _logger.LogDebug($"Starting {member.InstancePath} addition");

                // Skip if member is typeless
                if (member.DataType == null)
                {
                    _logger.LogDebug($"{member.InstancePath} is missing data type");
                    continue;
                }

                // Create the field and add to the structure
                PlcVariableTypeInfo memberInfo = new(CreateType(member.DataType))
                {
                    Offset = member.ByteOffset,
                    Comment = member.Comment
                };
                memberInfo.DataType = memberInfo.Name;
                memberInfo.Name = member.InstanceName;
                plcVariableTypeInfo.Children.Add(memberInfo);
                plcVariableTypeInfo.IsBlockWriteAllowed &= memberInfo.IsBlockWriteAllowed;

                _logger.LogDebug($"{member.InstancePath} added with next offset at {member.ByteOffset}");
            }

            // Cache the type and return
            _cache[dataType.FullName] = plcVariableTypeInfo;

            _logger.LogDebug($"{dataType.FullName} created successfully");
            return plcVariableTypeInfo;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IArrayType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            // Bail out if the array element is null
            if (dataType.ElementType == null)
            {
                throw new InvalidOperationException($"{dataType.FullName} is missing an element type");
            }

            _logger.LogDebug($"Starting {dataType.FullName} array creation");

            PlcVariableTypeInfo type = new(CreateType(dataType.ElementType))
            {
                Name = dataType.Name,
                IsArray = true,
                ArraySize = dataType.ByteSize / dataType.ElementType.ByteSize,
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };
            type.DataType += "[]";
            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IPointerType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} pointer creation");

            PlcVariableTypeInfo type = new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                IsAddressType = true,
                IsReadOnly = true,
                IsBlockWriteAllowed = false,
                DataType = dataType.ByteSize == 4 ? "uint" : "ulong",
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };

            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IEnumType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            // Bail out if the enum has no base type
            if (dataType.BaseType == null)
            {
                throw new InvalidOperationException($"{dataType.FullName} is missing a base type");
            }

            _logger.LogDebug($"Starting {dataType.FullName} enum creation");

            PlcVariableTypeInfo type = new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                IsEnum = true,
                DataType = "enum",
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };

            foreach (IEnumValue enumValue in dataType.EnumValues)
            {
                _logger.LogDebug($"Starting {enumValue.Name} addition");

                // Create the value
                PlcVariableTypeInfo enumerationValue = dataType.BaseType.Name.ToUpperInvariant() switch
                {
                    "BYTE" or "USINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = (byte)enumValue.RawValue[0],
                        BaseDataType = "byte",
                        Comment = enumValue.Comment
                    },
                    "SBYTE" or "SINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = (sbyte)enumValue.RawValue[0],
                        BaseDataType = "sbyte",
                        Comment = enumValue.Comment
                    },
                    "INT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = BitConverter.ToInt16(enumValue.RawValue, 0),
                        BaseDataType = "Int16",
                        Comment = enumValue.Comment
                    },
                    "UINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = BitConverter.ToUInt16(enumValue.RawValue, 0),
                        BaseDataType = "UInt16",
                        Comment = enumValue.Comment
                    },
                    "DINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = BitConverter.ToInt32(enumValue.RawValue, 0),
                        BaseDataType = "Int32",
                        Comment = enumValue.Comment
                    },
                    "UDINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = BitConverter.ToUInt32(enumValue.RawValue, 0),
                        BaseDataType = "UInt32",
                        Comment = enumValue.Comment
                    },
                    "LINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = BitConverter.ToInt64(enumValue.RawValue, 0),
                        BaseDataType = "Int64",
                        Comment = enumValue.Comment
                    },
                    "ULINT" => new()
                    {
                        Name = enumValue.Name,
                        IsEnumValue = true,
                        EnumValue = BitConverter.ToUInt64(enumValue.RawValue, 0),
                        BaseDataType = "UInt64",
                        Comment = enumValue.Comment
                    },
                    _ => throw new InvalidOperationException($"Enumeration {dataType.FullName} has a value beyond allowed limits of TwinCAT"),
                };
                type.Children.Add(enumerationValue);

                _logger.LogDebug($"{enumValue.Name} added to the enumeration");
            }

            // Cache the type and return
            _cache[dataType.FullName] = type;

            _logger.LogDebug($"{dataType.FullName} created successfully");
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IReferenceType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} reference creation");

            return new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                IsReadOnly = true,
                IsAddressType = true,
                IsBlockWriteAllowed = false,
                DataType = dataType.ByteSize == 4 ? "uint" : "ulong",
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IAliasType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            // Bail out if the alias has no base type
            if (dataType.BaseType == null)
            {
                throw new InvalidOperationException($"{dataType.FullName} is missing a base type");
            }

            _logger.LogDebug($"Starting {dataType.FullName} alias creation");

            // Create the real type, cache and send
            PlcVariableTypeInfo type = new(CreateType(dataType.BaseType))
            {
                DataType = dataType.Name,
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };
            _cache[dataType.FullName] = type;
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IInterfaceType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} interface creation");

            return new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                IsReadOnly = true,
                IsAddressType = true,
                IsBlockWriteAllowed = false,
                DataType = dataType.ByteSize == 4 ? "uint" : "ulong",
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IPrimitiveType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} primitive creation");

            // Manual conversion
            PlcVariableTypeInfo type = dataType.Name.ToUpperInvariant() switch
            {
                "BOOL" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    IsBoolean = true,
                    DataType = "bool",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "BYTE" or "USINT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "byte",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "SBYTE" or "SINT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "sbyte",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "UINT" or "WORD" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "UInt16",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "INT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "Int16",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "UDINT" or "TIME" or "DATE" or "DWORD" or "DATE_AND_TIME" or "DT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "UInt32",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "DINT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "Int32",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "ULINT" or "LTIME" or "LDATE" or "LWORD" or "UXINT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "UInt64",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "LINT" or "XINT" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "Int64",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "REAL" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "Single",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
                "LREAL" => new()
                {
                    Name = dataType.Name,
                    Size = dataType.ByteSize,
                    DataType = "double",
                    Comment = dataType.Comment,
                    Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
                },
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
        private PlcVariableTypeInfo CreateType(IStringType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} string creation");

            return new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                IsString = true,
                DataType = "string",
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(IUnionType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            _logger.LogDebug($"Starting {dataType.FullName} union creation");

            PlcVariableTypeInfo type = new()
            {
                Name = dataType.Name,
                Size = dataType.ByteSize,
                DataType = "union",
                Offset = 0,
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };

            foreach (IField member in dataType.Fields)
            {
                _logger.LogDebug($"Starting {member.InstancePath} addition");

                // Skip if member is typeless
                if (member.DataType == null)
                {
                    _logger.LogDebug($"{member.InstancePath} is missing data type");
                    continue;
                }

                // Create the field
                if (member.DataType is IStringType)
                {
                    throw new NotImplementedException($"{member.InstancePath} is not able to be handled right now because it is a string and C# doesn't like unions with strings");
                }

                // Create the field and add to the union
                PlcVariableTypeInfo memberInfo = new(CreateType(member.DataType))
                {
                    Offset = 0,
                    Comment = member.Comment
                };
                memberInfo.DataType = memberInfo.Name;
                memberInfo.Name = member.InstanceName;
                type.Children.Add(memberInfo);
                type.IsBlockWriteAllowed &= memberInfo.IsBlockWriteAllowed;

                _logger.LogDebug($"{member.InstancePath} added to the union");
            }

            // Cache the type and return
            _cache[dataType.FullName] = type;

            _logger.LogDebug($"{dataType.FullName} created successfully");
            return type;
        }

        /// <summary>
        /// Creates the type
        /// </summary>
        /// <param name="dataType">data type to create</param>
        /// <returns>Marshal-able C# type</returns>
        private PlcVariableTypeInfo CreateType(ISubRangeType dataType)
        {
            // Shortcut through the cache
            if (_cache.TryGetValue(dataType.FullName, out PlcVariableTypeInfo? value))
            {
                return value;
            }

            // Bail out if the subrange has no base type
            if (dataType.BaseType == null)
            {
                throw new InvalidOperationException($"{dataType.FullName} is missing a base type");
            }

            _logger.LogDebug($"Starting {dataType.FullName} subrange creation");

            // Create the real type, cache and send
            PlcVariableTypeInfo type = new(CreateType(dataType.BaseType))
            {
                DataType = dataType.Name,
                Comment = dataType.Comment,
                Attributes = dataType.Attributes.ToDictionary(x => x.Name, x => x.Value)
            };
            _cache[dataType.FullName] = type;
            return type;
        }
        #endregion
    }
}
