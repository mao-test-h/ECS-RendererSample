//#define ENABLE_SCALE

namespace MainContents
{
    using System.Runtime.InteropServices;
    using UnityEngine;

    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Rendering;
    using Unity.Mathematics;
    using Unity.Transforms;

    using UnityRandom = UnityEngine.Random;

    public sealed class Bootstrap : MonoBehaviour
    {
        /// <summary>
        /// 表示データ
        /// </summary>
#pragma warning disable 0649
        [SerializeField] MeshInstanceRenderer _look;
#pragma warning restore 0649

        /// <summary>
        /// 表示領域のサイズ
        /// </summary>
        [SerializeField] Vector3 _boundSize = new Vector3(256f, 256f, 256f);

        /// <summary>
        /// 最大オブジェクト数
        /// </summary>
        [SerializeField] int _maxObjectNum = 100000;

        /// <summary>
        /// trueならDefaultWorldを使用
        /// </summary>
        [SerializeField] bool _isUseDefaultWorld = false;


        /// <summary>
        /// MonoBehaviour.Start
        /// </summary>
        void Start()
        {
            EntityManager entityManager = null;
            if (this._isUseDefaultWorld)
            {
                // DefaultWorld側で立ち上げられているEntityManagerを取得
                entityManager = World.Active.GetOrCreateManager<EntityManager>();

                // DefaultWorldだと最初からEndFrameTransformSystemとMeshInstanceRendererSystemが立ち上がっているのでCreateManagerを呼ぶ必要は無い。
            }
            else
            {
                // DefaultWorldを消す
                //      - 「UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP」と言うシンボルを定義する形でも止められるが、サンプルなので敢えてこうしている。
                World.DisposeAllWorlds();

                // 自作Worldの立ち上げ及び描画に必要なComponentSystemの設定
                World.Active = new World("Sample World");
                entityManager = World.Active.CreateManager<EntityManager>();

                // デフォルトで用意されているTransform周りのComponentSystem
                World.Active.CreateManager(typeof(EndFrameTransformSystem));

                // デフォルトで用意されているPureECSで描画を行うためのComponentSystemである「MeshInstanceRendererSystem」の補助クラス
                World.Active.CreateManager(typeof(RenderingSystemBootstrap));
            }


            // テスト用に表示するEntityのアーキタイプ
            // - ランダムな位置に表示させるための「Position」
            // - 表示データとなる「MeshInstanceRenderer」
            // 
            // ※後はデフォルトだとシンボルで区切って無効状態にしているが、
            // 　「ENABLE_SCALE」と言うシンボルを定義することでEntityの拡縮を有効にし、
            // 　サンプルの挙動に「各Entityのサイズをランダムな大きさに変更する」処理を付け加えることが可能となっている。
            var archetype = entityManager.CreateArchetype(
                ComponentType.Create<Position>(),
#if ENABLE_SCALE
                ComponentType.Create<Scale>(),
#endif
                ComponentType.Create<MeshInstanceRenderer>());

            // Entityの生成(各種ComponentData/SharedComponentDataの初期化)
            // やっている事としては以下のリンクを参照。
            // - https://qiita.com/pCYSl5EDgo/items/18f1827a5b323a7712d7
            var entities = new NativeArray<Entity>(_maxObjectNum, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                entities[0] = entityManager.CreateEntity(archetype);

                // MeshInstanceRendererに対するデータの設定
                // →この例ではInspectorから登録したデータをそのまま受け渡している。
                entityManager.SetSharedComponentData(entities[0], _look);
                unsafe
                {
                    var ptr = (Entity*)NativeArrayUnsafeUtility.GetUnsafePtr(entities);
                    var rest = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(ptr + 1, entities.Length - 1, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref rest, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
                    entityManager.Instantiate(entities[0], rest);
                }

                // 各種ComponentDataの設定
                for (int i = 0; i < this._maxObjectNum; ++i)
                {
                    // 移動の例. Calueに任意の座標を与えることで移動可能。
                    entityManager.SetComponentData(entities[i], new Position { Value = this.GetRandomPosition() });
#if ENABLE_SCALE
                    // 拡縮の例. Valueに任意のサイズを与えることで拡縮可能。
                    entityManager.SetComponentData(entities[i], new Scale { Value = UnityRandom.Range(0.1f, 2f) });
#endif
                }
            }
            finally
            {
                entities.Dispose();
            }

            if (this._isUseDefaultWorld) { return; }
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World.Active);
        }

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            World.DisposeAllWorlds();
        }

        /// <summary>
        /// ランダムな位置の取得
        /// </summary>
        float3 GetRandomPosition()
        {
            var halfX = this._boundSize.x / 2;
            var halfY = this._boundSize.y / 2;
            var halfZ = this._boundSize.z / 2;
            return new float3(
                UnityRandom.Range(-halfX, halfX),
                UnityRandom.Range(-halfY, halfY),
                UnityRandom.Range(-halfZ, halfZ));
        }
    }
}
