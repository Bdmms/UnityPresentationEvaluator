using System.Collections.Generic;
using System.IO;
using System;

/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 * 
 * This script writes samples to .wav files
 */

public class AudioWriter
{
    //DO NOT CHANGE (IMPORTANT TO FILE CREATION)
    private const string _CHUNCK_ID = "RIFF";
    private const string _FORMAT = "WAVE";
    private const string _SUB_CHUNK_1_ID = "fmt ";
    private const string _SUB_CHUNK_2_ID = "data";
    private const int _SUB_CHUNK_1_SIZE = 16;  //PCM standard
    private const short _AUDIO_FORMAT = 1;     //Linear quantization
    private const short _NUM_CHANNELS = 2;     //Stereo
    private const short _BITS_PER_SAMPLE = 16; //16-bit

    //private static string _PATH = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/";

    //Generates a .wav file based on the sample of audio provided
    public static string WriteToWAV(string filename, int sampleRate, Queue<float> samples)
    {
        FileStream writer = new FileStream(filename, FileMode.Create, FileAccess.Write);

        WriteHeader(writer, filename, sampleRate, samples.Count);
        
        //Write data
        foreach(float sample in samples)
        {
            short value = 0;
            if (sample > 0)
                value = (short)(sample * 0x7FFF);
            else
                value = (short)(-sample * 0x7FFF + 0x7FFF);
            WriteShort(writer, value);  //LEFT CHANNEL
            WriteShort(writer, value);  //RIGHT CHANNEL
        }

        writer.Close();

        return filename;
    }

    //Writes the header of the .wav file
    private static void WriteHeader(FileStream write, string filename, int sampleRate, int size)
    {
        //INTRO DATA
        int byteRate = (sampleRate * _NUM_CHANNELS * _BITS_PER_SAMPLE) / 8;
        short blockAlign = (short)(_NUM_CHANNELS * _BITS_PER_SAMPLE / 8);
        //ADDITIONS DO NOT EXIST WHEN USING PCM
        int subChunk2Size = size * _NUM_CHANNELS * _BITS_PER_SAMPLE / 8;
        int chunkSize = 4 + (8 + _SUB_CHUNK_1_SIZE) + (8 + subChunk2Size);

        //Writing Data
        WriteString(write, _CHUNCK_ID);
		WriteInt(write, chunkSize);
        WriteString(write, _FORMAT);
        WriteString(write, _SUB_CHUNK_1_ID);
		WriteInt(write, _SUB_CHUNK_1_SIZE);
        WriteShort(write, _AUDIO_FORMAT);
        WriteShort(write, _NUM_CHANNELS);
        WriteInt(write, sampleRate);
        WriteInt(write, byteRate);
        WriteShort(write, blockAlign);
        WriteShort(write, _BITS_PER_SAMPLE);
        WriteString(write, _SUB_CHUNK_2_ID);
		WriteInt(write, subChunk2Size);
    }

    //Method for writing integers as four independent bytes
    private static void WriteInt(FileStream write, int i)
    {
        write.WriteByte((byte)(i%0xFF));
		write.WriteByte((byte)((i >> 8)%0xFF));
		write.WriteByte((byte)((i >> 16)%0xFF));
		write.WriteByte((byte)(i >> 24));
	}

    //Method for writing shorts as two independent bytes
    private static void WriteShort(FileStream write, short i)
    {
        write.WriteByte((byte)(i%0xFF));
        write.WriteByte((byte)(i >> 8));
    }

    private static void WriteString(FileStream write, string s)
    {
        foreach(char c in s)
            write.WriteByte((byte)c);
    }
}
