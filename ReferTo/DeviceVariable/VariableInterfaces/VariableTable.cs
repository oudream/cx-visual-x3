using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VariableInterfaces;

namespace VariableInterfaces
{
    // 每个设备都有自己的变量表，用于存储设备属性的值，并提供更新通知，以便其他组件可以更新设备属性
    // 这个类是线程安全的，可以在多个线程间共享
    // VariableTable 归属于 VariableManager
    public class VariableTable
    {
        private readonly object lockObject = new object();
        private Dictionary<string, IVariableInfo> variables = new Dictionary<string, IVariableInfo>();

        public string DeviceName { get; private set; }

        public Type DeviceType { get; private set; }

        public VariableTable(string deviceName, Type deviceType)
        {
            this.DeviceName = deviceName;
            this.DeviceType = deviceType;
        }

        public void UpdateVariable<TValue>(string variableName, TValue newValue)
        {
            VariableChangeType changeType = VariableChangeType.ScanUpdate;
            lock (lockObject)
            {
                VariableInfo<TValue> variableInfo;
                if (variables.TryGetValue(variableName, out var variable))
                {
                    variableInfo = variable as VariableInfo<TValue>;
                    if (variableInfo != null)
                    {
                        variableInfo.LastUpdated = TimeHelper.GetNow();
                        if (!variableInfo.Value.Equals(newValue))
                        {
                            variableInfo.Value = newValue;
                            changeType = VariableChangeType.ValueChange;
                        }
                    }
                    else
                    {
                        // 类型不匹配
                        throw new InvalidOperationException($"变量[{variableName}]找到，但类型不匹配，请检查设置值时的类型是否正确！");
                    }
                }
                else
                {
                    variableInfo = new VariableInfo<TValue>(newValue, TimeHelper.GetNow());
                    variables[variableName] = variableInfo;
                    changeType = VariableChangeType.VariableCreation;
                }
            }
            VariableManager.PushUpdate(DeviceName, variableName, changeType);
        }

        public TValue GetVariable<TValue>(string variableName)
        {
            lock (lockObject)
            {
                if (variables.TryGetValue(variableName, out var variable))
                {
                    var variableInfo = variable as VariableInfo<TValue>;
                    if (variableInfo != null)
                    {
                        return variableInfo.Value;
                    }
                    else
                    {
                        // Handle type mismatch
                        throw new InvalidOperationException($"变量[{variableName}]找到, 但类型不匹配.");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"变量[{variableName}]找不到.");
                }
            }
        }

        public bool TryGetVariable<TValue>(string variableName, out TValue value)
        {
            lock (lockObject)
            {
                if (variables.TryGetValue(variableName, out var variable))
                {
                    var variableInfo = variable as VariableInfo<TValue>;
                    if (variableInfo != null)
                    {
                        value = variableInfo.Value;
                        return true;
                    }
                }
            }
            value = default(TValue);
            return false;
        }

        public (T, long) GetVariable2<T>(string variableName)
        {
            lock (lockObject)
            {
                if (variables.TryGetValue(variableName, out var variable))
                {
                    var variableInfo = variable as VariableInfo<T>;
                    if (variableInfo != null)
                    {
                        return (variableInfo.Value, variableInfo.LastUpdated);
                    }
                    else
                    {
                        // Handle type mismatch
                        throw new InvalidOperationException($"变量[{variableName}]找到, 但类型不匹配.");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"变量[{variableName}]找不到.");
                }
            }
        }

        public bool TryGetVariable2<TValue>(string variableName, out TValue value, out long lastUpdated)
        {
            lock (lockObject)
            {
                if (variables.TryGetValue(variableName, out var variable))
                {
                    var variableInfo = variable as VariableInfo<TValue>;
                    if (variableInfo != null)
                    {
                        value = variableInfo.Value;
                        lastUpdated = variableInfo.LastUpdated;
                        return true;
                    }
                }
            }
            value = default(TValue);
            lastUpdated = 0;
            return false;
        }

        public (T, long, bool) GetVariable3<T>(string variableName)
        {
            lock (lockObject)
            {
                if (variables.TryGetValue(variableName, out var variable))
                {
                    var variableInfo = variable as VariableInfo<T>;
                    if (variableInfo != null)
                    {
                        return (variableInfo.Value, variableInfo.LastUpdated, variableInfo.HasInitialValue);
                    }
                    else
                    {
                        // Handle type mismatch
                        throw new InvalidOperationException($"变量[{variableName}]找到, 但类型不匹配.");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"变量[{variableName}]找不到.");
                }
            }
        }

        public bool TryGetVariable3<TValue>(string variableName, out TValue value, out long lastUpdated, out bool hasInitialValue)
        {
            lock (lockObject)
            {
                if (variables.TryGetValue(variableName, out var variable))
                {
                    var variableInfo = variable as VariableInfo<TValue>;
                    if (variableInfo != null)
                    {
                        value = variableInfo.Value;
                        lastUpdated = variableInfo.LastUpdated;
                        hasInitialValue = variableInfo.HasInitialValue;
                        return true;
                    }
                }
            }
            value = default(TValue);
            lastUpdated = 0;
            hasInitialValue = false;
            return false;
        }

        public void RemoveVariable(string variableName)
        {
            lock (lockObject)
            {
                variables.Remove(variableName);
            }
        }

        public void Clear()
        {
            lock (lockObject)
            {
                variables.Clear();
            }
        }

        public bool HasVariable(string variableName)
        {
            lock (lockObject)
            {
                return variables.ContainsKey(variableName);
            }
        }

        public List<string> GetVariableNames()
        {
            lock (lockObject)
            {
                return variables.Keys.ToList();
            }
        }

        public int GetVariableCount()
        {
            lock (lockObject)
            {
                return variables.Count;
            }
        }

        public void AddVariable<TValue>(string variableName, TValue value, bool hasInitialValue)
        {
            lock (lockObject)
            {
                variables[variableName] = new VariableInfo<TValue>(value, hasInitialValue);
            }
        }

        // 返回不满足条件的第一个变量名称
        public (bool, string) AllVariablesHaveInitialAndUpdatedValues()
        {
            lock (lockObject)
            {
                foreach (var entry in variables)
                {
                    if (entry.Value is IVariableInfo info && info.HasInitialValue)
                    {
                        if (info.LastUpdated <= 0)
                        {
                            return (false, entry.Key);
                        }
                    }
                }
            }
            return (true, string.Empty); // 所有变量都满足条件，返回true和空字符串
        }

    }

}
