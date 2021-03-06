﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoodAI.Core.Task
{
    public interface IMyExecutable
    {
        void Execute();
        bool Enabled { get; }        
        uint SimulationStep { get; set; }
        string Name { get; }
    }
}
