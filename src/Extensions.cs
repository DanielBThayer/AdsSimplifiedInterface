using Microsoft.Extensions.Logging;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TwinCAT.TypeSystem;

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// Extensions used in the interface class
    /// </summary>
    public static class Extensions
    {
        #region Data Type Conversion
        /// <summary>
        /// Converts an object from bytes to the object
        /// </summary>
        /// <typeparam name="T">Data type of the result</typeparam>
        /// <param name="data">Byte array of the object</param>
        /// <returns>Resulting object</returns>
        public static T CastByBytes<T>(this byte[] data)
        {
            // Verify the data is not invalid
            ArgumentNullException.ThrowIfNull(data);

            // Handle special cases
            if (typeof(T).IsArray)
            {
                // Get the element type and a method to cast by bytes
                Type elementType = typeof(T).GetElementType() ?? throw new InvalidCastException($"Array ({typeof(T).FullName}) of unknown element type is not convertible");
                MethodInfo cast = typeof(Extensions).GetMethod("CastByBytes") ?? throw new InvalidOperationException($"Unable to get method for converting value to data type {typeof(T).FullName}");
                cast = cast.MakeGenericMethod(elementType);

                // Create the list to hold it all
                IList elements = (IList)(Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType)) ?? throw new InvalidOperationException($"Unable to create a list of the element type {elementType.FullName}"));

                for (int offset = 0; offset < data.Length;)
                {
                    // Get the new element
                    var element = cast.Invoke(null, [data[offset..]])!;
                    elements.Add(element);

                    // Offset the data
                    offset += SizeOf(elementType, element);
                }

                // Convert to an array
                Array array = Array.CreateInstance(elementType, elements.Count);
                elements.CopyTo(array, 0);

                return (T)(object)array;
            }
            else if (typeof(T) == typeof(string))
            {
                string value = Encoding.UTF8.GetString(data);
                return (T)(object)value[..value.IndexOf('\0')];
            }
            else if (typeof(T) == typeof(bool))
            {
                return (T)(object)(data[0] > 0);
            }
            else if (typeof(T).IsEnum)
            {
                Type underType = typeof(T).GetEnumUnderlyingType();

                if (underType == typeof(UInt16))
                {
                    return (T)(object)BitConverter.ToUInt16(data, 0);
                }
                else if (underType == typeof(UInt32))
                {
                    return (T)(object)BitConverter.ToUInt32(data, 0);
                }
                else if (underType == typeof(UInt64))
                {
                    return (T)(object)BitConverter.ToUInt64(data, 0);
                }
                else if (underType == typeof(Int16))
                {
                    return (T)(object)BitConverter.ToInt16(data, 0);
                }
                else if (underType == typeof(Int32))
                {
                    return (T)(object)BitConverter.ToInt32(data, 0);
                }
                else if (underType == typeof(Int64))
                {
                    return (T)(object)BitConverter.ToInt64(data, 0);
                }
                else
                {
                    return (T)(object)data[0];
                }
            }

            int sizeOfT = Marshal.SizeOf(typeof(T));
            if (data.Length < sizeOfT)
            {
                throw new ArgumentException($"Size mismatch between bytes received ({data.Length}) and size of {typeof(T).FullName} ({sizeOfT})");
            }

            T thisStructure;
            GCHandle dataPtr = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                thisStructure = (T)(Marshal.PtrToStructure(dataPtr.AddrOfPinnedObject(), typeof(T)) ?? throw new InvalidOperationException($"Unable to convert byte array to {typeof(T).FullName}"));
            }
            finally
            {
                dataPtr.Free();
            }

            return thisStructure;
        }

        private static int SizeOf(Type? type, object? element)
        {
            if (type == null || element == null)
            {
                return 0;
            }
            else if (type == typeof(bool))
            {
                return 1;
            }
            else if (type.IsArray)
            {
                return ((Array)element).Length * SizeOf(type.GetElementType(), ((Array)element).GetValue(0));
            }
            else if (type.IsEnum)
            {
                return Marshal.SizeOf(type.GetEnumUnderlyingType());
            }

            return Marshal.SizeOf(element);
        }
        #endregion

        #region Beckhoff ADS Extensions
        /// <summary>
        /// Does a deep search for a symbol
        /// </summary>
        /// <param name="symbols">Group of symbols to search</param>
        /// <param name="InstancePath">Path of the variable to find</param>
        /// <returns>Found symbol or null</returns>
        public static ISymbol? FindSymbol(this ISymbolCollection<ISymbol> symbols, string InstancePath)
        {
            // Protect against bad calls
            if (string.IsNullOrEmpty(InstancePath) || string.IsNullOrWhiteSpace(InstancePath))
            {
                return null;
            }

            // Check each symbol
            foreach (ISymbol symbol in symbols)
            {
                if (InstancePath.Equals(symbol.InstancePath, StringComparison.OrdinalIgnoreCase))
                {
                    // Symbol found, stop search
                    return symbol;
                }
                else if (InstancePath.Contains(symbol.InstancePath + '.', StringComparison.OrdinalIgnoreCase) && symbol.SubSymbols.Count > 0)
                {
                    // Dive deeper because the symbol's parent was found
                    return FindSymbol(symbol.SubSymbols, InstancePath);
                }
                else if (InstancePath.Contains(symbol.InstancePath + '[', StringComparison.OrdinalIgnoreCase) && symbol.SubSymbols.Count > 0)
                {
                    // Dive deeper because the symbol may be an index of an array
                    return FindSymbol(symbol.SubSymbols, InstancePath);
                }
            }

            return null;
        }
        #endregion

        #region Log System
#pragma warning disable IDE0079 // Remove unnecessary suppression, to work around a bug in static analysis
#pragma warning disable CA2254 // Template should be a static expression
        /// <summary>
        /// Logs an error message, where the message can be an interpolated string
        /// </summary>
        /// <param name="logger">Log System Instance</param>
        /// <param name="message">Message to log</param>
        public static void LogError(this ILogger logger, string message)
        {
            // Protect against bad calls
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(logger);

            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.Log(LogLevel.Error, message);
            }
        }

        /// <summary>
        /// Logs a warning message, where the message can be an interpolated string
        /// </summary>
        /// <param name="logger">Log System Instance</param>
        /// <param name="message">Message to log</param>
        public static void LogWarning(this ILogger logger, string message)
        {
            // Protect against bad calls
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(logger);

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.Log(LogLevel.Warning, message);
            }
        }

        /// <summary>
        /// Logs an information message, where the message can be an interpolated string
        /// </summary>
        /// <param name="logger">Log System Instance</param>
        /// <param name="message">Message to log</param>
        public static void LogInfo(this ILogger logger, string message)
        {
            // Protect against bad calls
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(logger);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.Log(LogLevel.Information, message);
            }
        }

        /// <summary>
        /// Logs a debug message, where the message can be an interpolated string
        /// </summary>
        /// <param name="logger">Log System Instance</param>
        /// <param name="message">Message to log</param>
        public static void LogDebug(this ILogger logger, string message)
        {
            // Protect against bad calls
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(logger);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.Log(LogLevel.Debug, message);
            }
        }

        /// <summary>
        /// Logs a debug message, where the message can be an interpolated string
        /// </summary>
        /// <param name="logger">Log System Instance</param>
        /// <param name="message">Message to log</param>
        public static void LogStackTrace(this ILogger logger, string? trace)
        {
            // Protect against bad calls
            ArgumentNullException.ThrowIfNull(logger);

            if (logger.IsEnabled(LogLevel.Debug) && !string.IsNullOrEmpty(trace))
            {
                logger.Log(LogLevel.Debug, $"Stack Trace:{Environment.NewLine}{trace}");
            }
        }
#pragma warning restore CA2254 // Template should be a static expression
#pragma warning restore IDE0079 // Remove unnecessary suppression
        #endregion
    }
}
