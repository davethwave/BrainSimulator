﻿using GoodAI.Core.Nodes;
using GoodAI.Core.Utils;
using ManagedCuda.BasicTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoodAI.Core.Memory
{
    public class MyMemoryBlockSerializer
    {        
        private byte[] buffer = new byte[8192];

        private static string GetTempStorage(MyProject project)
        {
            return Path.GetTempPath() +  "\\bs_temporal\\" + project.Name + ".statedata";
        }

        public static void ClearTempStorage(MyProject project)
        {
            if (TempDataExists(project))
            {
                Directory.Delete(GetTempStorage(project), true);
                MyLog.INFO.WriteLine(project.Name + ": Temporal data deleted.");
            }
            else
            {
                MyLog.WARNING.WriteLine(project.Name + ": No temporal data to delete.");
            }
        }

        public static void ClearTempData(MyNode node)
        {
            if (TempDataExists(node))
            {
                Directory.Delete(GetTempStorage(node.Owner) + "\\" + GetNodeFolder(node), true);
                MyLog.INFO.WriteLine(node.Name + ": Temporal data deleted.");
            }
            else
            {
                MyLog.WARNING.WriteLine(node.Name + ": No temporal data to delete.");
            }
        }

        public static void ExportTempStorage(MyProject project, string outputFolder)
        {
            string tempStorage = GetTempStorage(project);

            foreach (string path in Directory.GetDirectories(tempStorage, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(path.Replace(tempStorage, outputFolder));
            }
            
            foreach (string filePath in Directory.GetFiles(tempStorage, "*.mb", SearchOption.AllDirectories))
            {
                File.Copy(filePath, filePath.Replace(tempStorage, outputFolder), true);
            }
        }

        public static string GetNodeFolder(MyNode node)
        {
            return node.GetType().Name + "_" + node.Id;
        }

        public static string GetFileName(MyAbstractMemoryBlock memoryBlock)
        {
            return memoryBlock.Name + ".mb";
        }

        public static bool TempDataExists(MyProject project)
        {
            return Directory.Exists(GetTempStorage(project));
        }

        public static bool TempDataExists(MyNode node)
        {
            return Directory.Exists(GetTempStorage(node.Owner) + "\\" + GetNodeFolder(node));            
        }

        private static string ResolveFilePath(MyAbstractMemoryBlock memoryBlock, string globalDataFolder)
        {
            string fileName = GetTempStorage(memoryBlock.Owner.Owner) + "\\" + GetNodeFolder(memoryBlock.Owner) + "\\" + GetFileName(memoryBlock);

            if (!File.Exists(fileName))
            {
                MyWorkingNode node = memoryBlock.Owner as MyWorkingNode;                

                if (!string.IsNullOrEmpty(node.DataFolder))
                {
                    fileName = node.DataFolder;                    
                }
                else if (!string.IsNullOrEmpty(globalDataFolder))
                {
                    fileName = globalDataFolder + "\\" + GetNodeFolder(memoryBlock.Owner);
                }
                else
                {
                    throw new FileNotFoundException("No data folder defined.");
                }

                fileName += "\\" + GetFileName(memoryBlock);
            }

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("Memory block file not found: " + fileName);
            }

            return fileName;
        }

        public void LoadBlock(MyAbstractMemoryBlock memoryBlock, string globalDataFolder)
        {
            int length = buffer.Length;
            SizeT size = memoryBlock.GetSize();

            while (size > length)
            {
                length *= 2;
            }

            if (length != buffer.Length)
            {
                buffer = new byte[length];
            }

            try
            {
                string filePath = ResolveFilePath(memoryBlock, globalDataFolder);
                long fileSize = new FileInfo(filePath).Length;

                if (fileSize != size)
                {
                    throw new InvalidDataException("Different size of a stored memory block (" + fileSize + " B != " + size + " B)");                        
                }

                BinaryReader reader = new BinaryReader(File.OpenRead(filePath));
                reader.Read(buffer, 0, size);
                reader.Close();

                memoryBlock.Fill(buffer);                    
            }
            catch (Exception e)
            {
                MyLog.WARNING.WriteLine("Memory block loading failed (" + memoryBlock.Owner.Name + "." + memoryBlock.Name + "): " + e.Message);
            }            
        }

        public void SaveBlock(MyAbstractMemoryBlock memoryBlock)
        {
            int length = buffer.Length;
            SizeT size = memoryBlock.GetSize();

            while (size > length)
            {
                length *= 2;
            }

            if (length != buffer.Length)
            {
                buffer = new byte[length];
            }
            memoryBlock.GetBytes(buffer);
            
            string tempFolder = GetTempStorage(memoryBlock.Owner.Owner) + "\\" + GetNodeFolder(memoryBlock.Owner);
            Directory.CreateDirectory(tempFolder);

            string filePath = tempFolder + "\\" + GetFileName(memoryBlock);

            try
            {
                BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create));

                writer.Write(buffer, 0, size);
                writer.Close();
            }
            catch (Exception e)
            {
                MyLog.WARNING.WriteLine("Memory block saving failed (" + memoryBlock.Owner.Name + "." + memoryBlock.Name + "): " + e.Message);
            } 
        }
    }
}
