using System;
using System.IO;
using System.Linq;

namespace SADXOpenStates
{
    public static class SaveStateSerialization
    {
        public static void SerialiseStates(SaveState[] states)
        {
            using (BinaryWriter binWriter =
                new BinaryWriter(File.Open("save.states", FileMode.Create)))
            {
                int realStates = states.Count(s => s != null);
                
                binWriter.Write((byte)3); // Version number
                binWriter.Write((byte)realStates); // Number of active states
                
                for (int i = 0; i < 10; i++)
                {
                    SaveState state = states[i];

                    if (state == null)
                    {
                        continue;
                    }
                    
                    binWriter.Write((byte)i); // Write one byte for the savestate index
                    
                    binWriter.Write(state.pos); // 0xC
                    binWriter.Write(state.rot); // 0xC
                    binWriter.Write(state.speed); // 0xC
                    
                    binWriter.Write(state.hover); // 0x2
                    binWriter.Write(state.lives); // 0x1
                    binWriter.Write(state.rings); // 0x2
                    
                    binWriter.Write(state.tFrames); // 0x1
                    binWriter.Write(state.tSeconds); // 0x1
                    binWriter.Write(state.tMins); // 0x1
                    
                    binWriter.Write(state.cameraStruct1); // 0x50
                    binWriter.Write(state.cameraStructOther); // 0x120
                    binWriter.Write(state.lastCameraPos); // 0xC
                    binWriter.Write(state.freeCamera); // 0x6
                    
                    binWriter.Write(state.animData); // 0x10
                    binWriter.Write(state.actionData); // 0x5
                    
                    binWriter.Write(state.curLevel); // 0x1
                    binWriter.Write(state.curAct); // 0x1
                    binWriter.Write(state.curChar); // 0x1
                }
            }
        }

        public static SaveState[] DeserializeStates()
        {
            SaveState[] states = new SaveState[10];
            
            using (BinaryReader binReader =
                new BinaryReader(File.Open("save.states", FileMode.Open)))
            {
                int version = binReader.ReadByte();
                int stateNumber = binReader.ReadByte();

                for (int i = 0; i < stateNumber; i++)
                {
                    int index = binReader.ReadByte();

                    states[index] = new SaveState
                    {
                        pos = binReader.ReadBytes(0xC),
                        rot = binReader.ReadBytes(0xC),
                        speed = binReader.ReadBytes(0xC),
                        
                        hover = binReader.ReadInt16(),
                        lives = binReader.ReadByte(),
                        rings = binReader.ReadInt16(),
                        
                        tFrames = binReader.ReadByte(),
                        tSeconds = binReader.ReadByte(),
                        tMins = binReader.ReadByte(),
                        
                        cameraStruct1 = binReader.ReadBytes(0x50),
                        cameraStructOther = binReader.ReadBytes(0x120),
                        lastCameraPos = binReader.ReadBytes(0xC),
                        freeCamera = binReader.ReadBytes(0x6),
                        
                        animData = binReader.ReadBytes(0x10),
                        actionData = binReader.ReadBytes(0x5),
                        
                        curLevel = binReader.ReadByte(),
                        curAct = binReader.ReadByte(),
                        curChar = binReader.ReadByte()
                    };
                }
            }

            return states;
        }
    }
}