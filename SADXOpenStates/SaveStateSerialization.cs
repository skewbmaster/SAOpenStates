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
                    
                    binWriter.Write(state.pos);
                    binWriter.Write(state.rot);
                    binWriter.Write(state.speed);
                    
                    binWriter.Write(state.hover);
                    binWriter.Write(state.lives);
                    binWriter.Write(state.rings);
                    
                    binWriter.Write(state.tFrames);
                    binWriter.Write(state.tSeconds);
                    binWriter.Write(state.tMins);
                    
                    //binWriter.Write(state.camera1);
                    binWriter.Write(state.freeCamera);
                    
                    binWriter.Write(state.animData);
                    binWriter.Write(state.actionData);
                    
                    binWriter.Write(state.curLevel);
                    binWriter.Write(state.curAct);
                    binWriter.Write(state.curChar);
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
                    Console.WriteLine(index);

                    states[index] = new SaveState
                    {
                        pos = binReader.ReadBytes(12),
                        rot = binReader.ReadBytes(12),
                        speed = binReader.ReadBytes(12),
                        hover = binReader.ReadInt16(),
                        lives = binReader.ReadByte(),
                        rings = binReader.ReadInt16(),
                        tFrames = binReader.ReadByte(),
                        tSeconds = binReader.ReadByte(),
                        tMins = binReader.ReadByte(),
                        //camera1 = binReader.ReadBytes(50),
                        freeCamera = binReader.ReadBytes(6),
                        animData = binReader.ReadBytes(16),
                        actionData = binReader.ReadBytes(5),
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