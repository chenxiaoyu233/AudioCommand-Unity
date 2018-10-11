using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices; // for P/Invoke
using System.IO;
using System.Threading;

namespace AudioControl
{

    public class CXYNN {
        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static void InitCXYNN();

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static int Predict();

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static int testCS();

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static void InitMfcc(int len);

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static void SetValue(int idx, Int16 val);

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static void AddFrame();

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static void SetPrev(int idx, Int16 val);

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static void calcProb();

        [DllImport("Assets/Plugins/audio-recognize/libAudio.dylib")]
        public extern static double readProb(int idx);
    }

    //在C#侧完成一些辅助工作
    public class AudioAux {
        // 将AudioClip中存储的-1.0f ~ 1.0f 之间的浮点数, 转换为计算MFCC时需要的Int16
        static public Int16[] ConvertToInt16(AudioClip clip) {
            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);
            Int16[] intData = new Int16[clip.samples];

            int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < samples.Length; i++) {
                intData[i] = (short)(samples[i] * rescaleFactor);
            }

            return intData;
        }
    }

    public class PosteriorHandler {
        public static int smoothWindowSize = 30;
        public static int confidentWindowSize = 100;
        public static int caseNumber = 10;

        static private Queue< double[] > smoothWindow = new Queue<double[]>(); // window for smooth, store pij
        static private Queue< double[] > confidentWindow = new Queue<double[]>();// window for confident, store p'ij
        static double[] curFame;

        public static void addFrame( double[] newFrame ) {
            smoothWindow.Enqueue(newFrame);
            if (smoothWindow.Count > smoothWindowSize) smoothWindow.Dequeue();
            double[] tmp = new double[caseNumber];
            foreach (double[] cur in smoothWindow) {
                for (int i = 0; i < caseNumber; i++) tmp[i] += cur[i];
            }
            for (int i = 0; i < caseNumber; i++) tmp[i] /= smoothWindow.Count;
            confidentWindow.Enqueue(tmp);
            if (confidentWindow.Count > confidentWindowSize) confidentWindow.Dequeue();
            curFame = tmp;
        }

        public static void updateWindow() {
            CXYNN.Predict();
            double[] tmp = new double[caseNumber];
            CXYNN.calcProb();
            for (int i = 0; i < caseNumber; i++) tmp[i] = CXYNN.readProb(i + 1);
            addFrame(tmp);
        }

        public static int Predict() {
            double maxx = curFame[0]; 
            int whe = 0;
            for (int i = 0; i < caseNumber; i++) {
                if (curFame[i] > maxx) {
                    maxx = curFame[i];
                    whe = i;
                }
            }
            return whe + 1;
        }

        public static double CalcConfident() {
            /* 
            double[] tmp = new double[caseNumber];
            for (int i = 0; i < caseNumber; i++) tmp[i] = 0; // init
            foreach (double[] cur in confidentWindow) {
                for (int i = 0; i < caseNumber; i++) 
                    tmp[i] = Math.Max(tmp[i], cur[i]);
            }
            double ret = 1;
            for (int i = 0; i < caseNumber; i++) ret *= tmp[i];
            return Math.Pow(ret, 1/(double)caseNumber);
            */
            double ret = 0;
            for (int i = 0; i < caseNumber; i++) ret = Math.Max(ret, curFame[i]);
            return ret;
        }
    }

    public class AudioController {
        private bool isAddPrevSamples;
        private Int16[] samples;
        public static int frameLength {
            get { return 10; } // 10ms per frame
        }
        string LogString;
        Thread NNThread;

        AudioClip clip;

        int sampleLen;
        int clipLen;

        public void InstructionAsync(AudioClip _clip) {
            clip = _clip;
            samples = AudioAux.ConvertToInt16(clip);
            clipLen = (int)clip.length;
            sampleLen = samples.Length;
            NNThread = new Thread(new ThreadStart(Instruction));
            NNThread.Start();
            //Instruction();
        }

        //预处理sample
        void prework() {
            int len = 0;
            bool flag = false;
            for (int i = 0; i < sampleLen; i++) {
                if (flag || samples[i] >= 1000 || samples[i] <= -1000) {
                    samples[len++] = samples[i];
                    flag = true;
                }
            }
            sampleLen = len;
            while (sampleLen > 0 && samples[sampleLen-1] > -1000 && samples[sampleLen-1] < 1000) sampleLen--;
            Debug.Log(sampleLen);
        }

        void Instruction() {
            int bufferLen = frameLength * 16000 / 1000;
            prework();
            for (int i = 0; i < 15 * 16; i++) {
                CXYNN.SetPrev(i, samples[i]);
            }
            int cnt = 0;
            int cutFlag = 0;
            for (int i = 15 * 16; i < sampleLen; i += bufferLen) {
                if( i + bufferLen > sampleLen) break;
                cnt += 1;
                instruction(i, i + bufferLen, cnt >= 30 && cutFlag % 3 == 0);
                cutFlag ++;
            }
            Debug.Log("Done");
        }
        void instruction(int begin, int end, bool needPredict) { // [begin, end)
            int len = end - begin;
            for(int i = 0; i < len; i++) {
                CXYNN.SetValue(i, samples[begin + i]);
            }
            CXYNN.AddFrame();

            PosteriorHandler.updateWindow();
            if (needPredict) {
                int ret = PosteriorHandler.Predict();
                double conf = PosteriorHandler.CalcConfident();
                Debug.Log(ret + " <- " + conf); // for test
                if (conf >= 0.3) {
                    Debug.LogError(ret);
                    PredictPool.AddPredict(ret);
                }
            }
        }

        public void Init() {
            CXYNN.InitCXYNN();
            CXYNN.InitMfcc(frameLength * 16000 / 1000);
        }

    }

    public class PredictPool {
        public static Queue<int> PredictSeq = new Queue<int>();
        public static int last;
        public static int count;
        public static void AddPredict(int idx) {
            -- idx;
            if (idx == last) count ++;
            else {
                if (count >= 3) {
                    PredictSeq.Enqueue(last);
                }
                last = idx; count = 1;
            }
        }

        public static void Init() {
            PredictSeq.Clear();
            last = -1;
            count = 0;
        }

        public static int[] GetArray() {
            Queue<int> q = new Queue<int>();
            q.Clear();
            foreach (int cur in PredictSeq) {
                q.Enqueue(cur);
            }
            if (count >= 3) q.Enqueue(last);
            return q.ToArray();
        }

    }

}