using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SADXOpenStates
{
    public class SaveState
    {
        public byte[] pos, rot, speed, animData, actionData;

        public byte[] cameraStruct1, cameraStructOther, lastCameraPos, freeCamera;

        public byte tFrames, tSeconds, tMins, lives, curLevel, curAct, curChar;
        public short rings, hover;

        public const SaveState Empty = default(SaveState);
        public SaveState(ref Process gameProc, ref int baseAddress)
        {
            IntPtr speedPointer = gameProc.GetFinalAddress(0x03B3CDF0, 0x0);
            
            pos = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B42E10, 0x20), 0xC);
            rot = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B42E10, 0x14), 0xC);

            speed = gameProc.ReadBytes(speedPointer + 0x38, 0xC);
            hover = gameProc.ReadInt16(speedPointer + 0x8);

            lives = gameProc.ReadByte(0x03B0EF34);
            rings = gameProc.ReadInt16(0x03B0F0E4);

            tFrames = gameProc.ReadByte(0x03B0EF35);
            tSeconds = gameProc.ReadByte(0x03B0F128);
            tMins = gameProc.ReadByte(0x03B0EF48);

            cameraStruct1 = gameProc.ReadBytes(gameProc.ReadInt32(0x03B2CBB0), 0x50); // The struct at the pointer
            cameraStructOther = gameProc.ReadBytes(0x3B2C9CC, 0x120); // The static struct
            lastCameraPos = gameProc.ReadBytes(0x3B2CA8C, 0xC);
            freeCamera = gameProc.ReadBytes(0x3B2C968, 0x6);
            
            animData = gameProc.ReadBytes(speedPointer + 0x124, 0x10);
            actionData = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B42E10, 0x0), 0x5);

            curLevel = gameProc.ReadByte(0x3B22DCC);
            curAct = gameProc.ReadByte(0x3B22DEC);
            curChar = gameProc.ReadByte(0x3B22DC0);

            
        }

        [JsonConstructor]
        public SaveState(int x) // Version 2 Constructor
        {
            
        }
        
        [JsonConstructor]
        public SaveState() // Version 1 Constructor
        {
            
        }
    }
}