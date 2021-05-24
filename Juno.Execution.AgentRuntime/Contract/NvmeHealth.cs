namespace Juno.Execution.AgentRuntime.Contract
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;

    /// <summary>
    /// Contract that defines health of an NVME drive.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class NvmeHealth : IEquatable<NvmeHealth>
    {
        private int? hashCode;

        /// <summary>
        /// Initializes an instance of <see cref="NvmeHealth"/>
        /// </summary>
        /// <param name="critical_warning">1 if there has been a critical warning 0 otherwise</param>
        /// <param name="temperature">The current temprature of the device.</param>
        /// <param name="available_spare">The number of available spares.</param>
        /// <param name="available_spare_threshold">The minimum number of spares aloud.</param>
        /// <param name="percentage_used">The percentage of the device being occupied.</param>
        /// <param name="data_units_read">The number of data units read.</param>
        /// <param name="data_units_written">The number of data units written.</param>
        /// <param name="host_reads">The number of host reads.</param>
        /// <param name="host_writes">The number of host writes.</param>
        /// <param name="controller_busy_time">The controller busy time.</param>
        /// <param name="power_cycles">The number of power cycles.</param>
        /// <param name="power_on_hours">Duration of time on.</param>
        /// <param name="unsafe_shutdowns">Number of unsafe shutdowns.</param>
        /// <param name="media_errors">Number of media errors.</param>
        /// <param name="num_err_log_entries">Number of error log entries.</param>
        /// <param name="warning_temp_time">Duration of device's temperature being in a warning state.</param>
        /// <param name="critical_comp_time">Duration of device entering a critical comp time.</param>
        [JsonConstructor]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Parameter names must match JSON property names.")]
        public NvmeHealth(
            long critical_warning, 
            long temperature, 
            long available_spare, 
            long available_spare_threshold, 
            long percentage_used, 
            long data_units_read, 
            long data_units_written,
            long host_reads,
            long host_writes,
            long controller_busy_time,
            long power_cycles,
            long power_on_hours,
            long unsafe_shutdowns,
            long media_errors,
            long num_err_log_entries,
            long warning_temp_time,
            long critical_comp_time)
        {
            this.CriticalWarning = critical_warning;
            this.Temperature = temperature;
            this.AvailableSpare = available_spare;
            this.AvailableSpareThreshold = available_spare_threshold;
            this.PercentageUsed = percentage_used;
            this.DataUnitsRead = data_units_read;
            this.DataUnitsWritten = data_units_written;
            this.HostReads = host_reads;
            this.HostWrites = host_writes;
            this.ControllerBusyTime = controller_busy_time;
            this.PowerCycles = power_cycles;
            this.PowerOnHours = power_on_hours;
            this.UnsafeShutdowns = unsafe_shutdowns;
            this.MediaErrors = media_errors;
            this.ErrorLogEntries = num_err_log_entries;
            this.WarningTemperatureTime = warning_temp_time;
            this.CriticalComputationTime = critical_comp_time;

        }

        /// <summary>
        /// Get critical warning status
        /// </summary>
        public long CriticalWarning { get; }
        
        /// <summary>
        /// Get current temperature
        /// </summary>
        public long Temperature { get; }
        
        /// <summary>
        /// Get number of available spares
        /// </summary>
        public long AvailableSpare { get; }
        
        /// <summary>
        /// Get number of available spare threshold
        /// </summary>
        public long AvailableSpareThreshold { get; }
        
        /// <summary>
        /// Get percentage use
        /// </summary>
        public long PercentageUsed { get; }
        
        /// <summary>
        /// Get data units read
        /// </summary>
        public long DataUnitsRead { get; }
        
        /// <summary>
        /// get data units written
        /// </summary>
        public long DataUnitsWritten { get; }
        
        /// <summary>
        /// Get host reads
        /// </summary>
        public long HostReads { get; }
        
        /// <summary>
        /// Get host writes
        /// </summary>
        public long HostWrites { get; }
        
        /// <summary>
        /// Get controller busy time
        /// </summary>
        public long ControllerBusyTime { get; }
        
        /// <summary>
        /// Get power cycles
        /// </summary>
        public long PowerCycles { get; }
        
        /// <summary>
        /// Get power on hours
        /// </summary>
        public long PowerOnHours { get; }
        
        /// <summary>
        /// Get unsafe shutdowns
        /// </summary>
        public long UnsafeShutdowns { get; }
        
        /// <summary>
        /// Get media errors
        /// </summary>
        public long MediaErrors { get; }
        
        /// <summary>
        /// Get error log entries
        /// </summary>
        public long ErrorLogEntries { get; }
        
        /// <summary>
        /// Get warning temperature
        /// </summary>
        public long WarningTemperatureTime { get; }
        
        /// <summary>
        /// Get critical computation time.
        /// </summary>
        public long CriticalComputationTime { get; }

        /// <summary>
        /// Evaluates the equality beteen two <see cref="NvmeHealth"/> instances
        /// </summary>
        /// <param name="other">The other <see cref="NvmeHealth"/> to evaluate equality against.</param>
        /// <returns>True/False if the two <see cref="NvmeHealth"/> are equal.</returns>
        public bool Equals(NvmeHealth other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Evaluates the equality between this and another object.
        /// </summary>
        /// <param name="obj">The object to compare equality against.</param>
        /// <returns>True/False if this and the object are equal.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            NvmeHealth other = obj as NvmeHealth;
            if (other == null)
            {
                return false;
            }

            return this.Equals(other);
        }

        /// <summary>
        /// Generates a unique hashcode for this.
        /// </summary>
        /// <returns>The hashcode for this.</returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                PropertyInfo[] properties = this.GetType().GetProperties();
                IEnumerable<long> propertyValues = properties.Where(p => p.PropertyType == typeof(long))
                    .Select(p => (long)p.GetValue(this));
                this.hashCode = propertyValues.Select(v => v.GetHashCode()).Aggregate((result, curr) => result += curr) + base.GetHashCode();
            }

            return this.hashCode.Value;
        }
    }
}
