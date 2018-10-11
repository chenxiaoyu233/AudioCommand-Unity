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
        // @param AudioClip clip
        // @param ArrayList buffer, 输出容器
        // @param int start, 开始偏移位置
        // @param int end, 结束偏移位置
        // 注意: [start, end)
        static public void ConvertToInt16(AudioClip clip, ArrayList buffer, int start, int end) {
            int len = end - start;
            float[] samples = new float[clip.samples];
            clip.GetData(samples, start);
            Int16 intData;

            int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < len; i++) {
                intData = (short)(samples[i] * rescaleFactor);
                if (intData >= 100 || intData <= -100) // 过滤声音很小的部分 (待调整)
                    buffer.Add(intData);
            }
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

        public static void Init() {
            smoothWindow.Clear();
            confidentWindow.Clear();
        }
    }

    public class AudioController {
        ArrayList samples;
        public static int frameLength = 10; // 10ms per frame
        public Thread NNThread;
        AudioClip clip;
        string curDevice;
        int lastPosition; // 本次读取clip中数据开始的位置

        // 异步语音识别
        // @param AudioClip _clip, 录音片段的引用
        // @param string _curDevice, 目标录音设备
        public void InstructionAsync(AudioClip _clip, string _curDevice) {
            clip = _clip;
            curDevice = _curDevice;
            samples = new ArrayList(); samples.Clear(); // 初始化samples
            lastPosition = 0;
            PosteriorHandler.Init();
            NNThread = new Thread(new ThreadStart(Instruction));
            NNThread.Start();
        }

        // 和clip同步当前的数据
        // @ret 能否继续读取clip
        public bool isRec;
        public void MainThreadSyncDataWithCilp() {
                isRec = Microphone.IsRecording(curDevice);
                if (!isRec) return;

                int curPosition = Microphone.GetPosition(curDevice);
                AudioAux.ConvertToInt16(clip, samples, lastPosition, curPosition);
                lastPosition = curPosition;
                //Debug.Log(isRec);
                Debug.Log("pos: " + lastPosition);
                Debug.Log("cnt: " + samples.Count);
        }

        bool syncDataWithClip() {
            return isRec;
        }

        void Instruction() {
            // 计算每一帧所包含的sample的数量
            int bufferLen = frameLength * 16000 / 1000;

            // 等待输入足够的音频片段
            while(samples.Count < 15 * 16 && syncDataWithClip()) {}

            if (samples.Count < 15 * 16) {
                Debug.Log("do not have enough info, exit");
                return;
            }

            // Mfcc 填充 <= 我们使用的Mfcc代码中需要预先进行填充, 以保证最初的几帧得到的数据是有效的
            for (int i = 0; i < 15 * 16; i++) {
                CXYNN.SetPrev(i, (Int16)samples[i]);
            }

            // 识别过程
            int cnt = 0;
            int cutFlag = 0;
            for (int i = 15 * 16; syncDataWithClip() || i + bufferLen - 1 < samples.Count; ) {
                if (i + bufferLen > samples.Count) continue;
                cnt += 1;
                instruction(i, i + bufferLen, cnt >= 30 && cutFlag % 3 == 0);
                i += bufferLen;
                cutFlag ++;
            }

            // for debug
            Debug.Log("Done");
        }
        void instruction(int begin, int end, bool needPredict) { // [begin, end)
            int len = end - begin;
            for(int i = 0; i < len; i++) {
                CXYNN.SetValue(i, (Int16) samples[begin + i]);
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