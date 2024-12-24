using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.TypeSystem;
using TwinCAT.TypeSystem;
using TwinCAT.TypeSystem.Generic;

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// ADS Interface
    /// </summary>
    public class AdsInterface : IDisposable
    {
        #region ADS Session Variables
        /// <summary>
        /// Session for the actual ADS communications
        /// </summary>
        private readonly AdsSession session;
        /// <summary>
        /// Connection to the PLC
        /// </summary>
        private readonly AdsConnection connection;
        /// <summary>
        /// AMS Net ID of the PLC
        /// </summary>
        private readonly string amsNetId;
        /// <summary>
        /// Port of the PLC
        /// </summary>
        private readonly int port;
        /// <summary>
        /// Frequency the PLC will be scanned for changes
        /// </summary>
        private readonly int scanRateInterval;
        /// <summary>
        /// Data type creator for creating C# versions of PLC data types
        /// </summary>
        private readonly AdsTypeCreator dataTypeCreater;
        /// <summary>
        /// Mapping of instance paths and their actions to execute when a change is detected
        /// </summary>
        private readonly ConcurrentDictionary<string, List<VariableNotification>> notifications;
        /// <summary>
        /// Cache of the symbol to instance name match
        /// </summary>
        private readonly ConcurrentDictionary<string, ISymbol> symbolCache;
        /// <summary>
        /// Timer for scanning the PLC for changes
        /// </summary>
        private readonly Timer scanTimer;
        #endregion

        #region Non-Session Variables
        /// <summary>
        /// Logger for the ADS Interface
        /// </summary>
        private readonly ILogger<AdsInterface> logger;
        /// <summary>
        /// Indicates if the object is disposed
        /// </summary>
        private bool disposedValue;
        #endregion

        #region Constructors
        /// <summary>
        /// ADS Interface main constructor
        /// </summary>
        /// <param name="AmsNetId">AMS Net ID of the PLC</param>
        /// <param name="Port">Port of the PLC</param>
        /// <param name="logger">Log system</param>
        /// <param name="scanFrequency">Time, in ms, between PLC scans for notifications</param>
        public AdsInterface(string AmsNetId, int Port, ILogger<AdsInterface> logger, int scanFrequency = 100)
        {
            // Copy construction variables to local
            amsNetId = AmsNetId;
            port = Port;
            this.logger = logger;
            scanRateInterval = scanFrequency;

            // Create the notification variables and symbol cache
            notifications = new ConcurrentDictionary<string, List<VariableNotification>>(StringComparer.OrdinalIgnoreCase);
            symbolCache = new ConcurrentDictionary<string, ISymbol>(StringComparer.OrdinalIgnoreCase);

            // Create the data type creator
            dataTypeCreater = new AdsTypeCreator(logger);

            // Create ADS session
            session = new AdsSession(AmsNetId, Port);
            session.ConnectionStateChanged += Session_ConnectionStateChanged;
            connection = (AdsConnection)session.Connect();
            session.Settings.SymbolLoader.SymbolsLoadMode = SymbolsLoadMode.Flat;
            session.Settings.SymbolLoader.AutomaticReconnection = true;
            session.Settings.SymbolLoader.ValueUpdateMode = TwinCAT.ValueAccess.ValueUpdateMode.Immediately;
            session.SymbolServer.ResetCachedSymbolicData();

            // Setup the scan timer
            scanTimer = new Timer(ScanTime, null, 0, scanRateInterval);
        }

        /// <summary>
        /// Constructor for being dynamically injected
        /// </summary>
        /// <param name="config">Configuration system for the application</param>
        /// <param name="logger">Log system</param>
        public AdsInterface(IConfiguration config, ILogger<AdsInterface> logger)
        {
            // Copy construction variables to local
            this.logger = logger;

            // Create the notification variables and symbol cache
            notifications = new ConcurrentDictionary<string, List<VariableNotification>>(StringComparer.OrdinalIgnoreCase);
            symbolCache = new ConcurrentDictionary<string, ISymbol>(StringComparer.OrdinalIgnoreCase);

            // Create the data type creator
            dataTypeCreater = new AdsTypeCreator(logger);

            // Fetch the configuration from the configuration manager
            amsNetId = config.GetValue<string>("ADS:NetId") ?? string.Empty;
            port = config.GetValue<int>("ADS:Port");
            port = port == 0 ? 851 : port;
            scanRateInterval = config.GetValue<int>("ADS:ScanRateMultiple");

            // Create ADS session
            if (string.IsNullOrEmpty(amsNetId))
            {
                // Local machine
                session = new AdsSession(port);
            }
            else
            {
                // Defined route to internal or external machine
                session = new AdsSession(amsNetId, port);
            }
            session.ConnectionStateChanged += Session_ConnectionStateChanged;
            connection = (AdsConnection)session.Connect();
            session.Settings.SymbolLoader.SymbolsLoadMode = SymbolsLoadMode.Flat;
            session.Settings.SymbolLoader.AutomaticReconnection = true;
            session.Settings.SymbolLoader.ValueUpdateMode = TwinCAT.ValueAccess.ValueUpdateMode.Immediately;
            session.SymbolServer.ResetCachedSymbolicData();

            // Setup the scan timer
            scanTimer = new Timer(ScanTime, null, 0, scanRateInterval);
        }
        #endregion

        #region PLC Scanner
        /// <summary>
        /// Scans the PLC for changes to variables on the notification list
        /// </summary>
        /// <param name="state">Not used</param>
        private void ScanTime(object? state)
        {
            // Skip if disposed or the PLC is not running
            if (disposedValue || session.Disposed)
            {
                return;
            }
            if (!session.IsConnected || session.ConnectionState != ConnectionState.Connected)
            {
                return;
            }

            // Set the update time
            long currentScanTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            // Scan for the notifications required
            ConcurrentDictionary<uint, ISymbol> symbols = [];
            Parallel.ForEach(notifications, (notification) =>
            {
                // Filter out the variables not needing to be read
                VariableNotification? variable = notification.Value.FirstOrDefault((s) => Math.Abs(s.LastUpdateTime - currentScanTime) >= s.ActionUpdateRate);
                if (variable == null)
                {
                    return;
                }

                symbols.TryAdd(variable.Handle, variable.Symbol);
            });

            // Get the values
            Dictionary<ISymbol, byte[]> results;
            try
            {
                results = ReadGroup(symbols.ToDictionary());
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception while reading values during PLC scan: {ex.Message}");
                logger.LogStackTrace(ex.StackTrace);
                return;
            }

            // Spin a new thread to allow the scanner to finish in a timely manner
            Task.Factory.StartNew(() =>
            {
                // Loop, in parallel over the results to notify anyone about the updated value
                Dictionary<ISymbol, byte[]> localResults = results;
                Parallel.ForEach(localResults, (result) =>
                {
                    try
                    {
                        if (!notifications.TryGetValue(result.Key.InstancePath, out List<VariableNotification>? notification))
                        {
                            // Notification was removed since last requested
                            return;
                        }

                        // Filter to only ones for this scan
                        notification = notification.Where(s => Math.Abs(s.LastUpdateTime - currentScanTime) >= s.ActionUpdateRate)
                                                   .ToList();

                        // Call each notification
                        foreach (VariableNotification variableNotification in notification)
                        {
                            try
                            {
                                variableNotification.VariableUpdate(result.Value);
                            }
                            catch (Exception e)
                            {
                                logger.LogError($"Exception while calling notification callback: {e.Message}");
                                logger.LogStackTrace(e.StackTrace);
                            }

                            variableNotification.LastUpdateTime = currentScanTime;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (session.Disposed || disposedValue)
                        {
                            return;
                        }

                        logger.LogError($"Exception while processing notification: {ex.Message}");
                        logger.LogStackTrace(ex.StackTrace);
                    }
                });
            });
        }
        #endregion

        #region ADS Session Events
        /// <summary>
        /// Session state has changed
        /// </summary>
        /// <param name="sender">Object raising the event</param>
        /// <param name="e">Event information</param>
        private void Session_ConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            // Log the change
            logger.LogInfo($"{amsNetId}:{port} ADS State Change: {e.OldState} -> {e.NewState}");

            // Report the exception, if there was one
            if (e.Exception != null)
            {
                logger.LogDebug($"Connection exception detected.  Message: {e.Exception.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{e.Exception.StackTrace}");
            }

            switch (e.NewState)
            {
                case ConnectionState.Lost:
                case ConnectionState.None:
                case ConnectionState.Disconnected:
                    // Clear all internal cache, the PLC might be changing
                    dataTypeCreater.ResetCache();
                    symbolCache.Clear();
                    break;
                case ConnectionState.Connected:
                    // Re-validate notifications, removing ones that don't exist anymore
                    long currentScanTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    ConcurrentBag<string> deadVariables = [];
                    ConcurrentDictionary<uint, ISymbol> symbols = [];
                    Parallel.ForEach(notifications, (notification) =>
                    {
                        // Filter out the variables not needing to be read
                        VariableNotification? variable = notification.Value.FirstOrDefault((s) => Math.Abs(s.LastUpdateTime - currentScanTime) >= s.ActionUpdateRate);
                        if (variable == null)
                        {
                            // Unable to get the symbol, kill the notifications
                            deadVariables.Add(notification.Key);
                            return;
                        }

                        symbols.TryAdd(variable.Handle, variable.Symbol);
                    });

                    // Remove the dead variables
                    foreach (string variableName in deadVariables)
                    {
                        notifications.TryRemove(variableName, out _);
                    }

                    // Update the variables
                    Dictionary<ISymbol, byte[]> results;
                    try
                    {
                        results = ReadGroup(symbols.ToDictionary());
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Exception while reading values during PLC scan: {ex.Message}");
                        logger.LogStackTrace(ex.StackTrace);
                        return;
                    }

                    // Loop, in parallel over the results to notify anyone about the updated value
                    Dictionary<ISymbol, byte[]> localResults = results;
                    Parallel.ForEach(localResults, (result) =>
                    {
                        foreach (VariableNotification variableNotification in notifications[result.Key.InstancePath].Where(s => Math.Abs(s.LastUpdateTime - currentScanTime) >= s.ActionUpdateRate).ToList())
                        {
                            try
                            {
                                variableNotification.Symbol = (IValueSymbol)result.Key;
                                variableNotification.VariableUpdate(result.Value);
                            }
                            catch (Exception e)
                            {
                                logger.LogError($"Exception while calling notification callback: {e.Message}");
                                logger.LogStackTrace(e.StackTrace);
                            }

                            variableNotification.LastUpdateTime = currentScanTime;
                        }
                    });
                    break;
            }
        }
        #endregion

        #region Notifications
        /// <summary>
        /// Remove all notifications for a PLC variable
        /// </summary>
        /// <param name="InstancePath">PLC variable to remove all notifications for</param>
        public void RemoveAllNotifications(string InstancePath)
        {
            // Clear all the actions
            notifications.Remove(InstancePath, out List<VariableNotification>? variables);

            if (variables != null && variables.Count > 0)
            {
                connection.TryDeleteVariableHandle(variables.First().Handle);
            }
        }

        /// <summary>
        /// Adds a notification with a callback and update frequency (typeless)
        /// </summary>
        /// <param name="InstancePath">Path to the PLC variable</param>
        /// <param name="Callback">Callback to call when changed</param>
        /// <param name="UpdateRate">Update frequency of the variable</param>
        /// <remarks>This will not immediately send out a notification.  If the current value needs to be send, call GetValue and send manually</remarks>
        public void AddNotification(string InstancePath, Action<string, byte[], byte[]> Callback, long UpdateRate = 100)
        {
            // Validate variable path
            ISymbol symbol = CheckInstancePath(InstancePath);

            // Add to the notification list
            if (!notifications.TryGetValue(InstancePath, out List<VariableNotification>? value))
            {
                value = ([]);
                notifications.AddOrUpdate(InstancePath, value, (key, oldValue) => { return value; });
            }

            try
            {
                // Get the handle
                uint Handle;
                if (value.Count == 0)
                {
                    Handle = connection.CreateVariableHandle(InstancePath);
                }
                else
                {
                    Handle = value.First().Handle;
                }

                // Update the notification
                value.Add(new VariableNotification(((IValueSymbol)symbol).ReadRawValue(), Callback, UpdateRate, (IValueSymbol)symbol, Handle));
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception while attempt to create a notification for {InstancePath}: {ex.Message}");
                logger.LogStackTrace(ex.StackTrace);
            }
        }

        /// <summary>
        /// Adds a notification with a callback and update frequency (typed)
        /// </summary>
        /// <param name="InstancePath">Path to the PLC variable</param>
        /// <param name="Callback">Callback to call when changed</param>
        /// <param name="UpdateRate">Update frequency of the variable</param>
        /// <remarks>This will not immediately send out a notification.  If the current value needs to be send, call GetValue and send manually</remarks>
        public void AddNotification<T>(string InstancePath, Action<string, T, T> Callback, long UpdateRate = 100)
        {
            // Validate variable path
            ISymbol symbol = CheckInstancePath(InstancePath);

            // Add to the notification list
            if (!notifications.TryGetValue(InstancePath, out List<VariableNotification>? value))
            {
                value = ([]);
                notifications.AddOrUpdate(InstancePath, value, (key, oldValue) => { return value; });
            }

            try
            {
                // Get the handle
                uint Handle;
                if (value.Count == 0)
                {
                    Handle = connection.CreateVariableHandle(InstancePath);
                }
                else
                {
                    Handle = value.First().Handle;
                }

                // Update the notification
                value.Add(new VariableNotification<T>(((IValueSymbol)symbol).ReadRawValue(), Callback, UpdateRate, (IValueSymbol)symbol, Handle));
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception while attempt to create a notification for {InstancePath}: {ex.Message}");
                logger.LogStackTrace(ex.StackTrace);
            }
        }
        #endregion

        #region Read Access
        /// <summary>
        /// Gets the value of the PLC variable
        /// </summary>
        /// <typeparam name="T">Data type of the PLC variable</typeparam>
        /// <param name="InstancePath">PLC variable path</param>
        /// <returns>Value from the PLC</returns>
        public T GetValue<T>(string InstancePath)
        {
            // Validate variable path
            ISymbol symbol = CheckInstancePath(InstancePath);

            // Convert the to value type
            IValueSymbol value = (IValueSymbol)symbol;

            // Return the value from the PLC
            return value.ReadRawValue()
                        .CastByBytes<T>();
        }

        /// <summary>
        /// Gets the value of the PLC variable
        /// </summary>
        /// <param name="InstancePath">PLC variable path</param>
        /// <returns>Value from the PLC</returns>
        public object? GetValue(string InstancePath)
        {
            // Fetch the data type
            Type T = GetDataType(InstancePath);

            // Fetch the GetValue method that is generic
            MethodInfo? getValue = GetType().GetMethod("GetValue", 1, [typeof(string)]);
            if (getValue == null)
            {
                logger.LogError($"Unable to get method for getting value of {InstancePath}, which is data type {T.FullName}");
                throw new InvalidOperationException($"Unable to get method for getting value of {InstancePath}, which is data type {T.FullName}");
            }

            // Generalize to correct type
            getValue = getValue.MakeGenericMethod(T);

            // Return the results
            return getValue.Invoke(this, [InstancePath]);
        }

        /// <summary>
        /// Reads a group of symbols from the PLC
        /// </summary>
        /// <param name="symbols">Dictionary of the handle and symbol to be read</param>
        /// <returns>Dictionary of the symbol and raw value from the PLC, if no error code was returned for the variable</returns>
        /// <exception cref="InvalidOperationException">Not enough data was returned for the variables requested</exception>
        private Dictionary<ISymbol, byte[]> ReadGroup(Dictionary<uint, ISymbol> symbols)
        {
            if (symbols.Count == 0)
            {
                // Nothing to read
                return [];
            }
            else if (symbols.Count > 500)
            {
                // Beckhoff has a sum read limit of 500, split the reads into groups of 500 or less
                int counter = 0;
                List<Dictionary<uint, ISymbol>> groups = symbols.GroupBy(x => counter++ / 500)
                                                                .Select(g => g.ToDictionary(h => h.Key, h => h.Value))
                                                                .ToList();

                // Read and concat the results into a single dictionary
                Dictionary<ISymbol, byte[]> results = [];
                foreach (Dictionary<uint, ISymbol> set in groups)
                {
                    var result = ReadGroup(set);
                    foreach (var pair in result)
                    {
                        results.Add(pair.Key, pair.Value);
                    }
                }

                // Return the filled dictionary
                return results;
            }

            // Create the group read command
            int size = 0;
            int readSize = 0;
            byte[] command = new byte[symbols.Count * sizeof(uint) * 3];
            foreach (var symbol in symbols)
            {
                foreach (byte b in BitConverter.GetBytes((uint)AdsReservedIndexGroup.SymbolValueByHandle))
                {
                    command.SetValue(b, size);
                    size++;
                }
                foreach (byte b in BitConverter.GetBytes((uint)symbol.Key))
                {
                    command.SetValue(b, size);
                    size++;
                }
                foreach (byte b in BitConverter.GetBytes((uint)symbol.Value.ByteSize))
                {
                    command.SetValue(b, size);
                    size++;
                }
                readSize += symbol.Value.ByteSize + sizeof(uint);
            }

            // Read the group of variables from the PLC
            byte[] data = new byte[readSize];
            Memory<byte> group = new(data);
            ReadOnlyMemory<byte> commands = new(command);
            int readBytes = connection.ReadWrite((uint)AdsReservedIndexGroup.SumCommandRead, (uint)symbols.Count, group, commands);

            // Only process the results, if the return value(s) is/are complete
            if (readSize >= readBytes)
            {
                Dictionary<ISymbol, byte[]> results = [];
                int valueOffset = sizeof(uint) * symbols.Count;
                int returnCodeOffset = 0;

                // Match each symbol with the result
                foreach (var symbol in symbols)
                {
                    // Get the return code
                    uint returnCode = BitConverter.ToUInt32(data, returnCodeOffset);

                    // Only add the new value if the return code was OK
                    if (returnCode == 0)
                    {
                        results.Add(symbol.Value, data[valueOffset..(valueOffset + symbol.Value.ByteSize)]);
                    }

                    // Move the offsets for the next variable's location
                    valueOffset += symbol.Value.ByteSize;
                    returnCodeOffset += sizeof(uint);
                }

                return results;
            }

            throw new InvalidOperationException($"Read failed with only {readBytes} out of {size}");
        }
        #endregion

        #region Write Access
        /// <summary>
        /// Write the value to the variable
        /// </summary>
        /// <typeparam name="T">Type of the variable</typeparam>
        /// <param name="InstancePath">Path to the variable</param>
        /// <param name="value">Value to write</param>
        public void SetValue<T>(string InstancePath, T value) where T : notnull
        {
            // Validate variable path
            ISymbol symbol = CheckInstancePath(InstancePath);

            // Send the value
            ((IValueSymbol)symbol).WriteValue(value);
        }

        /// <summary>
        /// Write the value to the variable
        /// </summary>
        /// <param name="InstancePath">Path to the variable</param>
        /// <param name="value">Value to write</param>
        public void SetValue(string InstancePath, object value)
        {
            // Validate variable path
            ISymbol symbol = CheckInstancePath(InstancePath);

            // Check for a bad data type
            ArgumentNullException.ThrowIfNull(symbol.DataType);

            // Verify object is valid for the PLC symbol
            Type symbolType = dataTypeCreater.CreateType(symbol.DataType);
            if (!symbolType.Name.Equals(value.GetType().Name, StringComparison.InvariantCultureIgnoreCase))
            {
                logger.LogError($"Data type mismatch.  Symbol is of {symbol.DataType.Name} and value if of {value.GetType().Name}");
                throw new InvalidOperationException($"Data type mismatch.  Symbol is of {symbol.DataType.Name} and value if of {value.GetType().Name}");
            }

            // Send the value
            ((IValueSymbol)symbol).WriteValue(value);
        }
        #endregion

        #region Data Type Generation
        /// <summary>
        /// Dynamically generates a data type for an instance path
        /// </summary>
        /// <param name="InstancePath">Path to PLC variable of the data type</param>
        /// <returns>Data type</returns>
        public Type GetDataType(string InstancePath)
        {
            // Validate variable path
            ISymbol symbol = CheckInstancePath(InstancePath);

            IDataType adsType = symbol.DataType ?? throw new KeyNotFoundException($"PLC variable {InstancePath} has no valid data type");

            return dataTypeCreater.CreateType(adsType);
        }

        /// <summary>
        /// Dynamically generates all data types declared in the PLC
        /// </summary>
        /// <returns>All PLC data types</returns>
        public List<Type> GetDataTypes()
        {
            List<Type> types = [];

            foreach (IDataType dataType in session.SymbolServer.DataTypes)
            {
                types.Add(dataTypeCreater.CreateType(dataType));
            }

            return types.Distinct()
                        .Where(s => !s.IsArray && !(s.Namespace ?? string.Empty).Equals("System", StringComparison.OrdinalIgnoreCase))
                        .ToList();
        }
        #endregion

        #region Variable Search
        /// <summary>
        /// Checks if a variable exists
        /// </summary>
        /// <param name="InstancePath">Variable to check</param>
        /// <returns>If the variable exists on the active PLC session</returns>
        public bool VariableExists(string InstancePath)
        {
            try
            {
                return CheckInstancePath(InstancePath) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get a list of variables
        /// </summary>
        /// <param name="startingPoint">Path to start at</param>
        /// <param name="PersistentOnly">Only return persistent variables</param>
        /// <returns>List of variables found</returns>
        public List<string> GetVariables(string startingPoint = "", bool PersistentOnly = false)
        {
            if (string.IsNullOrEmpty(startingPoint))
            {
                // No starting point, so search the entire PLC
                return [.. GetVariables([.. session.SymbolServer.Symbols], PersistentOnly)];
            }
            else
            {
                // Start at the defined starting point, if it exists
                List<ISymbol> symbols = [
                    session.SymbolServer.Symbols.FindSymbol(startingPoint) ?? throw new ArgumentException($"{startingPoint} does not exist on the PLC"),
                ];
                return [.. GetVariables(symbols, PersistentOnly)];
            }
        }

        /// <summary>
        /// Internal method for diving deeper into a variable tree during a search
        /// </summary>
        /// <param name="symbols">Symbols to check</param>
        /// <param name="PersistentOnly">Only return persistent variables</param>
        /// <returns>Array of variables found</returns>
        private static string[] GetVariables(List<ISymbol> symbols, bool PersistentOnly = false)
        {
            List<string> variables = [];

            foreach (ISymbol symbol in symbols)
            {
                if (!PersistentOnly || symbol.IsPersistent)
                {
                    variables.Add(symbol.InstancePath);
                }

                // Skip pointers and references to prevent a potential infinite recursion
                if (symbol.SubSymbols.Count > 0 && !(symbol.IsPointer || symbol.IsReference))
                {
                    variables.AddRange(GetVariables([.. symbol.SubSymbols], PersistentOnly));
                }
            }
            
            return [.. variables];
        }
        #endregion

        #region Common Methods
        /// <summary>
        /// Checks an instance path for issues with the symbol not existing, PLC not connected, etc
        /// </summary>
        /// <param name="InstancePath">Instance path to check</param>
        /// <returns>Symbol for the variable at the instance path</returns>
        /// <exception cref="ArgumentNullException">InstancePath was empty</exception>
        /// <exception cref="InvalidOperationException">Issue with PLC connection</exception>
        /// <exception cref="KeyNotFoundException">Instance Path doesn't exist</exception>
        private ISymbol CheckInstancePath(string InstancePath)
        {
            // Validate parameter
            if (string.IsNullOrEmpty(InstancePath))
            {
                throw new ArgumentNullException(nameof(InstancePath), "Value should be a valid path to a PLC variable");
            }

            // Validate session
            if (!session.IsConnected)
            {
                throw new InvalidOperationException("PLC Session is not in a valid state");
            }

            // Get symbol for the variable
            if (!symbolCache.TryGetValue(InstancePath, out ISymbol? symbol))
            {
                symbol = session.SymbolServer.Symbols.FindSymbol(InstancePath) ?? throw new KeyNotFoundException($"Unable to find PLC variable {InstancePath}");
                symbolCache[InstancePath] = symbol;
            }

            // Validate instance is a value type
            if (symbol is not IValueSymbol)
            {
                throw new InvalidOperationException($"{InstancePath} is not a valid value symbol");
            }

            return symbol;
        }
        #endregion

        #region Dispose
        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;

                if (disposing)
                {
                    session.Dispose();
                    scanTimer.Dispose();
                }
            }
        }

        /// <summary>
        /// Dispose of the object
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
