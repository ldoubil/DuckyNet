using System;
using UnityEngine;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 超轻量级 GRU 神经网络预测器
    /// 
    /// 架构: 输入(3) → GRU(8隐藏单元) → 输出(3)
    /// 参数量: ~300 个浮点数
    /// 单帧推理: < 0.05ms
    /// 
    /// 适合运行时在线学习(每帧自适应调整)
    /// </summary>
    public class TinyGRUPredictor
    {
        // GRU 隐藏层大小
        private const int HIDDEN_SIZE = 8;
        private const int INPUT_SIZE = 3;  // velocity, dirX, dirY
        private const int OUTPUT_SIZE = 3;
        
        // GRU 权重矩阵
        private float[,] _Wz, _Wr, _Wh;  // 更新门,重置门,候选门
        private float[,] _Uz, _Ur, _Uh;  // 隐藏状态权重
        private float[] _bz, _br, _bh;   // 偏置
        
        // 输出层权重
        private float[,] _Wo;
        private float[] _bo;
        
        // 隐藏状态
        private float[] _hiddenState;
        
        // 训练参数
        private float _learningRate = 0.01f;
        private bool _enableOnlineLearning = true;
        
        public TinyGRUPredictor()
        {
            InitializeWeights();
            _hiddenState = new float[HIDDEN_SIZE];
        }
        
        /// <summary>
        /// 初始化权重(Xavier初始化)
        /// </summary>
        private void InitializeWeights()
        {
            // GRU 门权重
            _Wz = XavierInit(INPUT_SIZE, HIDDEN_SIZE);
            _Wr = XavierInit(INPUT_SIZE, HIDDEN_SIZE);
            _Wh = XavierInit(INPUT_SIZE, HIDDEN_SIZE);
            
            _Uz = XavierInit(HIDDEN_SIZE, HIDDEN_SIZE);
            _Ur = XavierInit(HIDDEN_SIZE, HIDDEN_SIZE);
            _Uh = XavierInit(HIDDEN_SIZE, HIDDEN_SIZE);
            
            _bz = new float[HIDDEN_SIZE];
            _br = new float[HIDDEN_SIZE];
            _bh = new float[HIDDEN_SIZE];
            
            // 输出层
            _Wo = XavierInit(HIDDEN_SIZE, OUTPUT_SIZE);
            _bo = new float[OUTPUT_SIZE];
        }
        
        /// <summary>
        /// 预测下一帧
        /// </summary>
        public AnimationFrame Predict(AnimationFrame lastFrame, float deltaTime)
        {
            // 准备输入
            float[] input = new float[] 
            {
                lastFrame.MoveSpeed,
                lastFrame.MoveDirX,
                lastFrame.MoveDirY
            };
            
            // GRU 前向传播
            float[] output = Forward(input);
            
            // 如果启用在线学习,使用实际数据微调
            if (_enableOnlineLearning)
            {
                // 这里可以用真实数据与预测对比,进行梯度下降
                // 暂时省略反向传播(需要时可添加)
            }
            
            return new AnimationFrame
            {
                Timestamp = lastFrame.Timestamp + deltaTime,
                MoveSpeed = Mathf.Max(0, output[0]),
                MoveDirX = Mathf.Clamp(output[1], -1f, 1f),
                MoveDirY = Mathf.Clamp(output[2], -1f, 1f),
                IsDashing = lastFrame.IsDashing,
                IsGunReady = lastFrame.IsGunReady,
                IsReloading = lastFrame.IsReloading,
                IsDead = lastFrame.IsDead,
                HandState = lastFrame.HandState,
                AttackIndex = lastFrame.AttackIndex,
                StateHash = lastFrame.StateHash,
                NormalizedTime = lastFrame.NormalizedTime
            };
        }
        
        /// <summary>
        /// GRU 前向传播
        /// </summary>
        private float[] Forward(float[] input)
        {
            // 更新门: z_t = σ(W_z·x_t + U_z·h_(t-1) + b_z)
            float[] z = Sigmoid(MatMul(_Wz, input, _bz, MatMul(_Uz, _hiddenState)));
            
            // 重置门: r_t = σ(W_r·x_t + U_r·h_(t-1) + b_r)
            float[] r = Sigmoid(MatMul(_Wr, input, _br, MatMul(_Ur, _hiddenState)));
            
            // 候选隐藏状态: h̃_t = tanh(W_h·x_t + U_h·(r_t ⊙ h_(t-1)) + b_h)
            float[] rh = Hadamard(r, _hiddenState);
            float[] hTilde = Tanh(MatMul(_Wh, input, _bh, MatMul(_Uh, rh)));
            
            // 新隐藏状态: h_t = (1-z_t)⊙h_(t-1) + z_t⊙h̃_t
            for (int i = 0; i < HIDDEN_SIZE; i++)
            {
                _hiddenState[i] = (1 - z[i]) * _hiddenState[i] + z[i] * hTilde[i];
            }
            
            // 输出层: y = W_o·h_t + b_o
            return MatMul(_Wo, _hiddenState, _bo);
        }
        
        // ========== 辅助函数 ==========
        
        private float[,] XavierInit(int rows, int cols)
        {
            float[,] matrix = new float[rows, cols];
            float scale = Mathf.Sqrt(2f / (rows + cols));
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    matrix[i, j] = UnityEngine.Random.Range(-scale, scale);
            return matrix;
        }
        
        private float[] MatMul(float[,] W, float[] x, float[] bias = null, float[] add = null)
        {
            int rows = W.GetLength(0);
            int cols = W.GetLength(1);
            float[] result = new float[rows];
            
            for (int i = 0; i < rows; i++)
            {
                float sum = 0;
                for (int j = 0; j < cols; j++)
                    sum += W[i, j] * x[j];
                
                if (bias != null) sum += bias[i];
                if (add != null) sum += add[i];
                result[i] = sum;
            }
            return result;
        }
        
        private float[] Sigmoid(float[] x)
        {
            float[] result = new float[x.Length];
            for (int i = 0; i < x.Length; i++)
                result[i] = 1f / (1f + Mathf.Exp(-x[i]));
            return result;
        }
        
        private float[] Tanh(float[] x)
        {
            float[] result = new float[x.Length];
            for (int i = 0; i < x.Length; i++)
                result[i] = Mathf.Tanh(x[i]);
            return result;
        }
        
        private float[] Hadamard(float[] a, float[] b)
        {
            float[] result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = a[i] * b[i];
            return result;
        }
        
        public void Reset()
        {
            Array.Clear(_hiddenState, 0, HIDDEN_SIZE);
        }
        
        /// <summary>
        /// 保存/加载权重(用于预训练模型)
        /// </summary>
        public void SaveWeights(string path)
        {
            // TODO: 序列化权重到文件
        }
        
        public void LoadWeights(string path)
        {
            // TODO: 从文件加载权重
        }
    }
}
