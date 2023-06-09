﻿using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GPUParticleAttraction
{
    public class GPUParticleAttraction3D : MonoBehaviour
    {
        [System.Serializable]
        public struct ParticleData3D
        {
            public Vector3 position;
            public Vector3 velocity;
            public Color color;
            public float size;
            public float uniqueSpeed;
        }

        const int THREAD_NUM = 512;

        #region particle parameters
        // BUGS: 
        // if MaxParticleNum is not a multiple of THREAD_NUM, some particles don't move
        [Range(10000, 1048576)]
        public int MaxParticleNum = 1048576;
        [Range(0.01f, 0.1f)]
        public float ParticleSize = 0.1f;
        public float AttractStrength = 10f;
        public float MaxSpeed = 5.0f;
        public float AvoidWallStrength = 10.0f;
        public Vector3 WallCenter = Vector3.zero;
        public Vector3 WallSize = new Vector3(50, 50, 5);

        public Transform targetTrans;
        public float MovementThresh = 0.5f;
        #endregion

        public ComputeShader ParticleAttractCS;

        private ComputeBuffer _attractionBuffer;
        private ComputeBuffer _particleDataBuffer;
        private Vector2 prevMousePos;
        private int attractKernel;
        private int updateKernel;
        private int threadGroupSize;
        private float camDepth;
        
        private Vector3 _lastTargetPos;

        public ComputeBuffer GetParticleDataBuffer()
        {
            return _particleDataBuffer != null ? _particleDataBuffer : null;
        }

        public int GetMaxParticleNum()
        {
            return MaxParticleNum;
        }

        public Vector3 GetSimulationAreaCenter()
        {
            return WallCenter;
        }

        public Vector3 GetSimulationAreaSize()
        {
            return WallSize;
        }

        private void Start()
        {
            Application.targetFrameRate = -1;
            
            InitBuffer();

            // set kernel ID
            attractKernel = ParticleAttractCS.FindKernel("Attract");
            updateKernel = ParticleAttractCS.FindKernel("Update");

            // set thread group size
            threadGroupSize = Mathf.CeilToInt(MaxParticleNum / THREAD_NUM);

            // set camera depth which is necessary for getting the right mouse position
            camDepth = Mathf.Abs(Camera.main.transform.position.z - this.transform.position.z);
            
            if(targetTrans != null)
            {
                _lastTargetPos = targetTrans.position;
            }
        }

        private void Update()
        {
            // if (Input.GetMouseButton(0))
            // {
            //     var mousePos = Input.mousePosition;
            //     mousePos.z = camDepth;
            //     mousePos = Camera.main.ScreenToWorldPoint(mousePos);
            //
            //     var mouseVel = new Vector2( (mousePos.x - prevMousePos.x) / Time.deltaTime, (mousePos.y - prevMousePos.y) / Time.deltaTime );
            //     prevMousePos = mousePos;
            //
            //     AttractParticle(mousePos, mouseVel);
            // }

            if (targetTrans != null)
            {
                Vector3 currentPos = targetTrans.position;
                if (Vector3.Distance(_lastTargetPos, currentPos) > MovementThresh)
                {
                    Vector3 targetVel = new Vector3( (currentPos.x - _lastTargetPos.x) / Time.deltaTime, (currentPos.y - _lastTargetPos.y) / Time.deltaTime, (currentPos.z - _lastTargetPos.z) / Time.deltaTime );
                    AttractParticle(currentPos, targetVel);
                }
                _lastTargetPos = currentPos;
            }

            UpdateParticle();
        }

        private void OnDisable()
        {
            ReleaseBuffer();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(WallCenter, WallSize);
        }

        /// <summary>
        /// Initialize compute buffers
        /// </summary>
        private void InitBuffer()
        {
            _attractionBuffer   = new ComputeBuffer(MaxParticleNum, Marshal.SizeOf(typeof(Vector3)));
            _particleDataBuffer = new ComputeBuffer(MaxParticleNum, Marshal.SizeOf(typeof(ParticleData3D)));

            var attraction = new Vector3[MaxParticleNum];
            var particles  = new ParticleData3D[MaxParticleNum];
            for (int i = 0; i < MaxParticleNum; i++)
            {
                attraction[i] = Vector3.zero;
                 
                particles[i].position    = Random.insideUnitCircle * 5.0f;
                particles[i].velocity    = Random.insideUnitCircle * 0.1f;
                particles[i].color       = Color.white;
                particles[i].size        = ParticleSize;
                particles[i].uniqueSpeed = Random.Range(0.5f, 1.5f);
            }
            _attractionBuffer.SetData(attraction);
            _particleDataBuffer.SetData(particles);

            attraction = null;
            particles  = null;
        }

        /// <summary>
        /// execute Attract kernel
        /// </summary>
        private void AttractParticle(Vector3 targetPos, Vector3 targetVel)
        {
            ComputeShader cs = ParticleAttractCS;

            cs.SetFloat("_AttractStrength", AttractStrength);
            cs.SetFloat("_MaxSpeed", MaxSpeed);
            cs.SetVector("_TargetPos", targetPos);
            cs.SetVector("_TargetVel", targetVel);
            cs.SetBuffer(attractKernel, "_AttractionBuffer", _attractionBuffer);
            cs.SetBuffer(attractKernel, "_ParticleDataBufferRead", _particleDataBuffer);
            cs.Dispatch(attractKernel, threadGroupSize, 1, 1);
        }

        /// <summary>
        /// execute Update kernel
        /// </summary>
        private void UpdateParticle()
        {
            ComputeShader cs = ParticleAttractCS;

            cs.SetFloat("_DeltaTime", Time.deltaTime);
            cs.SetFloat("_AvoidWallStrength", AvoidWallStrength);
            cs.SetVector("_WallCenter", WallCenter);
            cs.SetVector("_WallSize", WallSize);
            // cs.SetVector("_MousePos", prevMousePos);
            cs.SetBuffer(updateKernel, "_AttractionBuffer", _attractionBuffer);
            cs.SetBuffer(updateKernel, "_ParticleDataBufferWrite", _particleDataBuffer);
            cs.Dispatch(updateKernel, threadGroupSize, 1, 1);
        }

        /// <summary>
        /// Release used compute buffers
        /// </summary>
        private void ReleaseBuffer()
        {
            new[] { _attractionBuffer, _particleDataBuffer }.ToList().ForEach( buffer =>
            {
                if (buffer != null)
                    buffer.Release();
                buffer = null;
            });
        }
    }
}