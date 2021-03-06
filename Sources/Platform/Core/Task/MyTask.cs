﻿using GoodAI.Core.Nodes;
using GoodAI.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCuda.BasicTypes;
using YAXLib;
using System.ComponentModel;
using System.Reflection;


namespace GoodAI.Core.Task
{
    [YAXSerializeAs("Task"), YAXSerializableType(FieldsToSerialize = YAXSerializationFields.AttributedFieldsOnly)]
    public abstract class MyTask :  IMyExecutable
    {        
        private bool m_enabled;

        [YAXSerializableField(DefaultValue = false), YAXAttributeForClass]
        public bool Enabled 
        {
            get { return m_enabled; }
            set 
            {
                m_enabled = value;
                
                if (value && GenericOwner != null)
                {
                    GenericOwner.DisableTaskGroup(this);
                }                
            } 
        }
        public uint SimulationStep { get; set; }

        public bool OneShot 
        {
            get { return GetInfo().OneShot; }
        }

        private MyWorkingNode m_genericOwner;

        public virtual MyWorkingNode GenericOwner 
        {
            get { return m_genericOwner; }
            internal set { m_genericOwner = value; }
        }

        public abstract void Init(Int32 nGPU);
        public abstract void Execute();
                
        private string m_name;
        public string Name
        {
            get
            {
                if (m_name == null)
                {
                    DescriptionAttribute attr = GetType().GetCustomAttribute<DescriptionAttribute>(false);
                    m_name = attr != null ? attr.Description : GetType().Name;
                }
                return m_name;
            }
        }

        private string m_taskGroupName = null;
        public string TaskGroupName
        {
            get
            {
                if (m_taskGroupName == null)
                {
                    PropertyInfo pInfo = GenericOwner.GetInfo().KnownTasks[this.PropertyName];
                    MyTaskGroupAttribute groupAttr = pInfo.GetCustomAttribute<MyTaskGroupAttribute>(true);
                    
                    m_taskGroupName = groupAttr != null ? groupAttr.Name : string.Empty;                    
                }
                return m_taskGroupName;
            }
        }

        [YAXSerializableField, YAXAttributeForClass]
        internal string PropertyName { get; set; }

        private static Dictionary<Type, MyTaskInfoAttribute> TASK_INFO = new Dictionary<Type, MyTaskInfoAttribute>();

        protected MyTaskInfoAttribute GetInfo()
        {
            InitTaskInfo();
            return TASK_INFO[GetType()];
        }

        private void InitTaskInfo()
        {
            Type type = GetType();

            if (!TASK_INFO.ContainsKey(type))
            {
                MyTaskInfoAttribute attr = type.GetCustomAttribute<MyTaskInfoAttribute>(false);

                if (attr == null)
                {
                    attr = new MyTaskInfoAttribute();
                }

                TASK_INFO[type] = attr;
            }
        }

        protected internal void InitPropertiesDefault()
        {
            foreach (PropertyInfo pInfo in GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                object defaultValue = pInfo.GetAttributeProperty((YAXSerializableFieldAttribute fa) => fa.DefaultValue);

                if (defaultValue != null)
                {
                    pInfo.SetValue(this, defaultValue);
                }
            }
        }

    }

    public abstract class MyTask<OwnerType> : MyTask where OwnerType : MyWorkingNode
    {
        public OwnerType Owner { get; private set; }

        public sealed override MyWorkingNode GenericOwner
        {
            get
            {
                return base.GenericOwner;
            }
            internal set
            {
                base.GenericOwner = value;
                Owner = (OwnerType)value;
            }
        }

        public MyTask()
        {
            Enabled = true;            
        }       

    }    
}
