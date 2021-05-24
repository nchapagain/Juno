namespace Juno.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using Microsoft.Azure.CRC.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Container class for contracts that are required to be defined as a parameter
    /// in the Juno System. 
    /// </summary>
    [JsonConverter(typeof(JunoParameterJsonConverter))]
    public class JunoParameter : IConvertible, IEquatable<JunoParameter>
    {
        private int? hashCode;

        /// <summary>
        /// Constructor for <see cref="JunoParameter"/>
        /// </summary>
        /// <param name="parameterType">the type of the object definition</param>
        /// <param name="definition">The content of the object</param>
        public JunoParameter(string parameterType, object definition)
        {
            parameterType.ThrowIfNullOrWhiteSpace(nameof(parameterType));
            definition.ThrowIfNull(nameof(definition));

            this.ParameterType = parameterType;
            this.Definition = definition;
        }

        /// <summary>
        /// The derived type of definition
        /// </summary>
        [JsonProperty(PropertyName = "parameterType")]
        public string ParameterType { get; }

        /// <summary>
        /// The object that is stored in this container
        /// </summary>
        [JsonProperty(PropertyName = "definition")]
        public object Definition { get; }

        /// <summary>
        /// This method allows the mapping from the
        /// IConvertible interface to the actual objects implementation.
        /// </summary>
        /// <param name="conversionType">The type to convert to</param>
        /// <param name="provider"></param>
        /// <returns></returns>
        public object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(JunoParameter))
            {
                return this;
            }

            return null;
        }

        /// <summary>
        /// Returns if this instance is equal to the other
        /// instance provided
        /// </summary>
        /// <param name="other">The other <see cref="JunoParameter"/>
        /// to asses equality against</param>
        /// <returns>True if the two instances are equal, false otherwise</returns>
        public bool Equals(JunoParameter other)
        {
            return other != null && this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Returns if this instance is equal to the other object provided
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to asses equality against</param>
        /// <returns>True if the two instances are equal, false otherwise</returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            JunoParameter itemDescription = obj as JunoParameter;

            return itemDescription != null && this.Equals(itemDescription);
        }

        /// <summary>
        /// Calculates the hashcode of this
        /// </summary>
        /// <returns>The hashcode</returns>
        public override int GetHashCode()
        {
            if (this.hashCode == null)
            {
                this.hashCode = new StringBuilder()
                    .Append(this.ParameterType)
                    .ToString().GetHashCode(StringComparison.OrdinalIgnoreCase);
                this.hashCode += this.Definition.GetHashCode();
            }

            return this.hashCode.Value;
        }

        /// <inheritdoc />
        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        /// <inheritdoc />
        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public string ToString(IFormatProvider provider)
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <inheritdoc />
        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }
    }
}
