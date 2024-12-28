using TwinCAT.TypeSystem;

namespace AdsSimplifiedInterface
{
    /// <summary>
    /// Stores the notification settings for a PLC variable
    /// </summary>
    /// <remarks>
    /// Constructs the notification with an initial value
    /// </remarks>
    /// <param name="InitialValue">Initial value</param>
    /// <param name="Callback">Callback to call when the value changes</param>
    /// <param name="UpdateRate">Update frequency of the variable</param>
    /// <param name="symbol">PLC Symbol for the variable</param>
    /// <param name="handle">Handle for the PLC variable</param>
    internal class VariableNotification<T>(byte[] InitialValue, Action<string, T, T> Callback, long UpdateRate, IValueSymbol symbol, uint handle) : VariableNotification(InitialValue, UpdateRate, symbol, handle)
    {
        #region Members
        /// <summary>
        /// Action to preform
        /// </summary>
        private Action<string, T, T> Notification { get; set; } = Callback;
        #endregion

        #region Handle Notifications
        /// <summary>
        /// Compare the old value and new value to see if a change happened.  If so, send the notification
        /// </summary>
        /// <param name="newValue">New value of the variable</param>
        public override void VariableUpdate(byte[] newValue)
        {
            // Compare the values and invoke the action if the value changed
            ReadOnlySpan<byte> oldValue = new(LastValue);
            ReadOnlySpan<byte> Value = new(newValue);
            if (!oldValue.SequenceEqual(Value))
            {
                Notification.Invoke(Symbol.InstancePath, LastValue.CastByBytes<T>(), newValue.CastByBytes<T>());
                LastValue = newValue;
            }
        }
        #endregion
    }
}
