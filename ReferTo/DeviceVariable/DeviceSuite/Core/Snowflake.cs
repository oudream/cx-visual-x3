using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    public static class SnowflakeManager
    {
        private static Dictionary<ObjectType, Snowflake> _generators = new Dictionary<ObjectType, Snowflake>();
        private const long WorkerId = 0; // 假设所有生成器使用相同的WorkerId
        private const int DatacenterIdBits = 5;
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;
        private const int SequenceBits = 12;
        private const int WorkerIdBits = 5;

        static SnowflakeManager()
        {
            foreach (ObjectType dataType in Enum.GetValues(typeof(ObjectType)))
            {
                _generators[dataType] = new Snowflake(WorkerId, dataType);
            }
        }

        public static long GenerateId(ObjectType dataType)
        {
            return _generators[dataType].NextId();
        }

        public static ObjectType GetDataTypeFromId(long id)
        {
            long maskedValue = (id >> DatacenterIdShift) & ((1 << DatacenterIdBits) - 1);
            return (ObjectType)maskedValue;
        }
    }

    public class Snowflake
    {
        private const long Twepoch = 1288834974657L;

        private const int WorkerIdBits = 5;
        private const int DatacenterIdBits = 5;
        private const int SequenceBits = 12;

        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);
        private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);

        private const int WorkerIdShift = SequenceBits;
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

        private const long SequenceMask = -1L ^ (-1L << SequenceBits);

        private long _lastTimestamp = -1L;

        public long WorkerId { get; protected set; }
        public long DatacenterId { get; protected set; }
        public long Sequence { get; private set; }

        public ObjectType DataType { get; protected set; }

        public Snowflake(long workerId, ObjectType dataType, long sequence = 0L)
        {
            WorkerId = workerId;
            DataType = dataType;
            Sequence = sequence;

            if (workerId > MaxWorkerId || workerId < 0)
            {
                throw new ArgumentException($"worker Id can't be greater than {MaxWorkerId} or less than 0");
            }
        }

        public long NextId()
        {
            lock (this)
            {
                var timestamp = TimeGen();

                if (timestamp < _lastTimestamp)
                {
                    throw new Exception($"Clock moved backwards. Refusing to generate id for {_lastTimestamp - timestamp} milliseconds");
                }

                if (_lastTimestamp == timestamp)
                {
                    Sequence = (Sequence + 1) & SequenceMask;

                    if (Sequence == 0)
                    {
                        timestamp = TilNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    Sequence = 0;
                }

                _lastTimestamp = timestamp;

                return ((timestamp - Twepoch) << TimestampLeftShift) |
                        ((long)DataType << DatacenterIdShift) |
                        (WorkerId << WorkerIdShift) | Sequence;
            }
        }

        private long TilNextMillis(long lastTimestamp)
        {
            var timestamp = TimeGen();
            while (timestamp <= lastTimestamp)
            {
                timestamp = TimeGen();
            }
            return timestamp;
        }

        private long TimeGen()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        
    }

}
