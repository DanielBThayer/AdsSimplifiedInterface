using TwinCAT.TypeSystem;

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// Stores the notification settings for a PLC variable
    /// </summary>
    internal class VariableNotification
    {
        #region Members
        /// <summary>
        /// PLC Symbol for the notification
        /// </summary>
        public IValueSymbol Symbol { get; set; }
        /// <summary>
        /// Action to preform
        /// </summary>
        private Action<string, byte[], byte[]> Notification { get; set; }
        /// <summary>
        /// Rate notification updates at
        /// </summary>
        public long ActionUpdateRate { get; set; }
        /// <summary>
        /// Value from the last update
        /// </summary>
        protected byte[] LastValue { get; set; }
        /// <summary>
        /// Last time the value was updated
        /// </summary>
        public long LastUpdateTime { get; set; }
        /// <summary>
        /// Variable handle from the PLC
        /// </summary>
        public uint Handle { get; set; }
        #endregion

        #region
        /// <summary>
        /// Main constructor for creating a typeless variable notification
        /// </summary>
        /// <param name="InitialValue">Initial value</param>
        /// <param name="CallBack">Callback to call when the value changes</param>
        /// <param name="UpdateRate">Update frequency of the variable</param>
        /// <param name="symbol">PLC Symbol for the variable</param>
        /// <param name="handle">Handle for the PLC variable</param>
        public VariableNotification(byte[] InitialValue, Action<string, byte[], byte[]> CallBack, long UpdateRate, IValueSymbol symbol, uint handle)
        {
            LastValue = InitialValue;
            ActionUpdateRate = UpdateRate;
            LastUpdateTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Notification = CallBack;
            Symbol = symbol;
            Handle = handle;
        }

        /// <summary>
        /// Constructor used by the typed variable notification
        /// </summary>
        /// <param name="InitialValue">Initial value</param>
        /// <param name="UpdateRate">Update frequency of the variable</param>
        /// <param name="symbol">PLC Symbol for the variable</param>
        /// <param name="handle">Handle for the PLC variable</param>
        public VariableNotification(byte[] InitialValue, long UpdateRate, IValueSymbol symbol, uint handle)
        {
            LastValue = InitialValue;
            ActionUpdateRate = UpdateRate;
            LastUpdateTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Notification = new Action<string, byte[], byte[]>((obj1, obj2, obj3) =>
            {
                return;
            });
            Symbol = symbol;
            Handle = handle;
        }
        #endregion

        #region Handle Notifications
        /// <summary>
        /// Compare the old value and new value to see if a change happened.  If so, send the notification
        /// </summary>
        /// <param name="newValue">New value of the variable</param>
        public virtual void VariableUpdate(byte[] newValue)
        {
            // Compare the values and invoke the action if the value changed
            ReadOnlySpan<byte> oldValue = new(LastValue);
            ReadOnlySpan<byte> Value = new(newValue);
            if (!oldValue.SequenceEqual(Value))
            {
                Notification.Invoke(Symbol.InstancePath, LastValue, newValue);
                LastValue = newValue;
            }
        }
        #endregion
    }
}
