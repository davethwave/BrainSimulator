﻿using GoodAI.Core.Memory;
using GoodAI.Core.Utils;
using GoodAI.Core.Signals;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YAXLib;
using GoodAI.Core.Task;
using GoodAI.Core.Nodes;
using GoodAI.Modules.NeuralNetwork.Group;

namespace GoodAI.Modules.NeuralNetwork.Layers
{

    public enum ConnectionType
    {
        NOT_SET,
        FULLY_CONNECTED,
        CONVOLUTION,
        ONE_TO_ONE
    }

    public enum ActivationFunctionType
    {
        NO_ACTIVATION,
        SIGMOID,
        IDENTITY,
        GAUSSIAN,
        RATIONAL_SIGMOID,
        RELU,
        TANH
    }

    public abstract class MyAbstractLayer : MyWorkingNode
    {
        // Properties
        [YAXSerializableField(DefaultValue = ActivationFunctionType.NO_ACTIVATION)]
        [MyBrowsable, Category("\tLayer")]
        public virtual ActivationFunctionType ActivationFunction { get; set; }

        [YAXSerializableField(DefaultValue = 128)]
        [MyBrowsable, Category("\tLayer")]
        public virtual int Neurons { get; set; }

        [YAXSerializableField(DefaultValue = 1)]
        [MyBrowsable, Category("Misc")]
        public int OutputColumnHint { get; set; }

        internal MyAbstractLayer PreviousLayer { get; set; }
        internal MyAbstractLayer NextLayer { get; set; }

        internal MyNeuralNetworkGroup ParentNetwork
        {
            get { return Parent as MyNeuralNetworkGroup; }
        }


        public virtual ConnectionType Connection
        {
            get { return ConnectionType.NOT_SET; }
        }


        #region Memory blocks
        // Memory blocks
        [MyInputBlock(0)]
        public MyMemoryBlock<float> Input
        {
            get { return GetInput(0); }
        }

        [MyOutputBlock(0)]
        public MyMemoryBlock<float> Output
        {
            get { return GetOutput(0); }
            set { SetOutput(0, value); }
        }

        public MyMemoryBlock<float> Delta { get; protected set; }
        #endregion

        //parameterless constructor
        public MyAbstractLayer() { }

        //Memory blocks size rules
        public override void UpdateMemoryBlocks()
        {
            Output.Count = Neurons;
            Output.ColumnHint = OutputColumnHint;
            Delta.Count = Neurons;
        }

        public override void Validate(MyValidator validator)
        {
            //            base.Validate(validator);
            validator.AssertError(Neurons > 0, this, "Number of neurons should be > 0");
            validator.AssertWarning(Connection != ConnectionType.NOT_SET, this, "ConnectionType not set for " + this);
        }

        //tasks
        public MyTask ForwardTask { get; protected set; }
        public MyTask DeltaBackTask { get; protected set; }
    }
}