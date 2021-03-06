﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

using TDCG;
using TDCGUtils;

namespace Tso2Pmd
{
    public class TransTso2Pmd
    {
        PmxFile pmd = new PmxFile();

        List<string> categories;
        List<bool> use_meshes = new List<bool>();

        Figure fig;
        List<TSOSubMesh> meshes;
        T2PMaterialList material_list;
        TemplateList template_list;
        CorrespondTableList cortable_list;

        public Figure Figure { get { return fig; } set { fig = value; } }
        //public PmxFile Pmd { get { return pmd; } }
        public bool UseOneBone { get;  set; }
        public string TextureFilePrefix { get; set; }
        public bool UseSpheremap { get; set; }
        public bool UseEdge { get; set; }
        public bool UniqueMaterial { get; set; }
        public List<string> Categories { set { categories = value; } }
        public List<bool> UseMeshes { set { use_meshes = value; } }
        public TemplateList TemplateList { set { template_list = value; } }
        public CorrespondTableList CorTableList { set { cortable_list = value; } }
          
        // -----------------------------------------------------
        // 表情設定リスト
        // -----------------------------------------------------
        Morphing morph = new Morphing();
      
        // 表情に関連するBoneの最小、及び最大
        const int FACE_BONE_MIN = 86;
        const int FACE_BONE_MAX = 135;

        public TransTso2Pmd()
        {
            morph.Load(Path.Combine(Application.StartupPath, @"表情"));
        }

        /// ヘッダー情報を入力します。
        public void InputHeader(string name, string comment)
        {
            if (name.Length > 9)
                throw new FormatException("モデル名が9文字を超えています。");
            if (comment.Length > 127)
                throw new FormatException("コメントが127文字を超えています。");

            pmd.header = new PMD_Header();
            pmd.header.name = name;
            pmd.header.comment = comment;
        }

        /// PMDファイルを出力します。
        public void SavePmdFile(string path)
        {
            pmd.Save(path);
        }
        
        /// マテリアル関係のファイルを出力します。
        public void OutputMaterialFile(string path, string name)
        {
            material_list.Save(path, name);
        }

        /// Figureを元にPmdFileを更新します。
        public void UpdatePmdFromFigure()
        {
            if (UseOneBone)
                UpdatePmdFromFigureWithOneBone();
            else
                UpdatePmdFromFigureWithHumanBone();
        }

        /// Figureを元にPmdFileを更新します。
        /// ボーンはセンターのみです。
        public void UpdatePmdFromFigureWithOneBone()
        {
            CorrespondTable cortable;
            cortable_list.BoneKind = CorrespondTableListBoneKind.one;
            cortable = cortable_list.GetCorrespondTable();

            PMD_DispGroup disp_group = cortable.boneDispGroups[0];//Root枠
            {
                PMD_BoneDisp disp = new PMD_BoneDisp();
                disp.bone_name = "センター";
                disp_group.disps.Add(disp);
            }

            // -----------------------------------------------------
            // ボーン情報
            // -----------------------------------------------------
            pmd.nodes = new PMD_Bone[2];

            // センター
            pmd.nodes[0] = new PMD_Bone();
            pmd.nodes[0].name = "センター";
            pmd.nodes[0].Kind = 1; // 1:回転と移動
            pmd.nodes[0].ParentName = null;
            pmd.nodes[0].TailName = "センター先";
            pmd.nodes[0].TargetName = null;
            pmd.nodes[0].position = new Vector3(0.0f, 5.0f, 0.0f);

            // センター先
            pmd.nodes[1] = new PMD_Bone();
            pmd.nodes[1].name = "センター先";
            pmd.nodes[1].Kind = 7; // 7:非表示
            pmd.nodes[1].ParentName = "センター";
            pmd.nodes[1].TailName = null;
            pmd.nodes[1].TargetName = null;
            pmd.nodes[1].position = new Vector3(0.0f, 0.0f, 0.0f);

            // -----------------------------------------------------
            // 予め、情報をコピーするmeshを選定し、並び替えておく
            // -----------------------------------------------------
            SelectMeshes();

            // -----------------------------------------------------
            // 頂点＆マテリアル
            // -----------------------------------------------------
            MakePMDVertices(null, 2);

            // -----------------------------------------------------
            // 表情
            // -----------------------------------------------------
            pmd.skins = new PMD_Skin[0];

            // -----------------------------------------------------
            // IK配列
            // -----------------------------------------------------
            pmd.iks = new PMD_IK[0];

            // -----------------------------------------------------
            // 表示枠
            // -----------------------------------------------------
            pmd.disp_groups = cortable.boneDispGroups;

            pmd.bodies = new PMD_RBody[0];
            pmd.joints = new PMD_Joint[0];
        }

        /// Figureを元にPmdFileを更新します。
        /// ボーンは人型です。
        public void UpdatePmdFromFigureWithHumanBone()
        {
            int mod_type = 0;

            if (fig.Tmo.nodes.Length == 227)
            {
                mod_type = 0;
            }
            else if (fig.Tmo.nodes.Length == 75)
            {
                mod_type = 1;
            }
            else
            {
                throw new FormatException("未対応のボーン構造です。\n人型以外を変換する場合は、\n出力ボーンに\"1ボーン\"を指定してください。");
            }

            CorrespondTable cortable;

            if (mod_type == 0)
            {
                cortable = cortable_list.GetCorrespondTable();
            }
            else
            {
                cortable_list.BoneKind = CorrespondTableListBoneKind.man;
                cortable = cortable_list.GetCorrespondTable();
            }
 
            // -----------------------------------------------------
            // ボーン情報
            // -----------------------------------------------------
            List<PMD_Bone> nodes = new List<PMD_Bone>();

            foreach (KeyValuePair<string, PMD_Bone> bone_kvp in cortable.boneStructure)
            {
                PMD_Bone bone = bone_kvp.Value;
                PMD_Bone pmd_b = new PMD_Bone();

                pmd_b.name = bone.name;
                pmd_b.name_en = bone.name_en;
                pmd_b.Kind = bone.Kind;
                pmd_b.ParentName = bone.ParentName;
                pmd_b.TailName = bone.TailName;
                pmd_b.TargetName = bone.TargetName;

                string bone_name = null;
                cortable.bonePositions.TryGetValue(pmd_b.name, out bone_name);
                if (bone_name != null)
                {
                    pmd_b.position
                        = Trans.CopyMat2Pos(fig.Tmo.FindNodeByName(bone_name).combined_matrix); // モデル原点からの位置
                }

                nodes.Add(pmd_b);
            }

            SortNodes(nodes);

            // -----------------------------------------------------
            // リストを配列に代入し直す
            pmd.nodes = nodes.ToArray();

            UpdateRootBonePosition();

            if (mod_type == 0)
            {
                UpdateEyesBonePosition();
            }

            UpdateIKTailBonePosition();

            // -----------------------------------------------------
            // 予め、情報をコピーするmeshを選定し、並び替えておく
            // -----------------------------------------------------
            SelectMeshes();

            // -----------------------------------------------------
            // 頂点
            // -----------------------------------------------------
            MakePMDVertices(cortable, mod_type);

            // -----------------------------------------------------
            // 表情
            // -----------------------------------------------------
            if (mod_type == 0)
            {
                InitializePMDFaces();
                MakePMDFaces();
            }
            else
            {
                InitializePMDFaces();
                pmd.skins = new PMD_Skin[0];
            }

            // -----------------------------------------------------
            // IK配列
            // -----------------------------------------------------
            pmd.iks = cortable.iks.ToArray();

            // -----------------------------------------------------
            // 表示枠
            // -----------------------------------------------------
            pmd.disp_groups = cortable.boneDispGroups;

            if (mod_type == 0)
            {
                PMD_DispGroup disp_group = pmd.disp_groups[1];//表情枠

                for (int i = 0; i < pmd.skins.Length; i++)
                {
                    PMD_SkinDisp skin_disp = new PMD_SkinDisp();
                    skin_disp.skin_id = (sbyte)i;
                    disp_group.disps.Add(skin_disp);
                }
            }

            if (mod_type == 0)
            {
                T2PPhysObjectList physOb_list = new T2PPhysObjectList(nodes);

                template_list.PhysObExecute(ref physOb_list);

                pmd.bodies = physOb_list.bodies.ToArray();
                pmd.joints = physOb_list.joints.ToArray();
            }
            else
            {
                pmd.bodies = new PMD_RBody[0];
                pmd.joints = new PMD_Joint[0];
            }
        }

        /// 親子関係を元に並び替える
        static void SortNodes(List<PMD_Bone> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                PMD_Bone node = nodes[i];
                //親より前に居る子を見つける
                for (int j = 0; j < i; j++)
                {
                    if (nodes[j].ParentName == node.name)
                    {
                        //親を削除
                        nodes.RemoveAt(i);
                        //親を子の直前に挿入
                        nodes.Insert(j, node);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// センターボーンの位置調整
        /// </summary>
        void UpdateRootBonePosition()
        {
            pmd.GetBoneByName("センター").position
                = new Vector3(
                    0.0f,
                    pmd.GetBoneByName("下半身").position.Y * 0.65f,
                    0.0f);
            pmd.GetBoneByName("センター先").position
                = new Vector3(
                    0.0f,
                    0.0f,
                    0.0f);
        }

        /// <summary>
        /// 両目ボーンの位置調整
        /// </summary>
        void UpdateEyesBonePosition()
        {
            pmd.GetBoneByName("両目").position
                = new Vector3(
                    0.0f,
                    pmd.GetBoneByName("左目").position.Y + pmd.GetBoneByName("左目").position.X * 4.0f,
                    pmd.GetBoneByName("左目").position.Z - pmd.GetBoneByName("左目").position.X * 2.0f);
            pmd.GetBoneByName("両目先").position
                = new Vector3(
                    pmd.GetBoneByName("両目").position.X,
                    pmd.GetBoneByName("両目").position.Y,
                    pmd.GetBoneByName("両目").position.Z - 1.0f);
        }

        /// <summary>
        /// IK先ボーンの位置調整
        /// </summary>
        void UpdateIKTailBonePosition()
        {
            pmd.GetBoneByName("左足ＩＫ先").position
                = new Vector3(
                    pmd.GetBoneByName("左足ＩＫ").position.X,
                    pmd.GetBoneByName("左足ＩＫ").position.Y,
                    pmd.GetBoneByName("左足ＩＫ").position.Z + 1.7f);
            pmd.GetBoneByName("右足ＩＫ先").position
                = new Vector3(
                    pmd.GetBoneByName("右足ＩＫ").position.X,
                    pmd.GetBoneByName("右足ＩＫ").position.Y,
                    pmd.GetBoneByName("右足ＩＫ").position.Z + 1.7f);

            pmd.GetBoneByName("左つま先").position.Y = 0.0f;
            pmd.GetBoneByName("左つま先ＩＫ").position.Y = 0.0f;
            pmd.GetBoneByName("左つま先ＩＫ先").position
                = new Vector3(
                    pmd.GetBoneByName("左つま先ＩＫ").position.X,
                    pmd.GetBoneByName("左つま先ＩＫ").position.Y - 1.0f,
                    pmd.GetBoneByName("左つま先ＩＫ").position.Z);

            pmd.GetBoneByName("右つま先").position.Y = 0.0f;
            pmd.GetBoneByName("右つま先ＩＫ").position.Y = 0.0f;
            pmd.GetBoneByName("右つま先ＩＫ先").position
                = new Vector3(
                    pmd.GetBoneByName("右つま先ＩＫ").position.X,
                    pmd.GetBoneByName("右つま先ＩＫ").position.Y - 1.0f,
                    pmd.GetBoneByName("右つま先ＩＫ").position.Z);
        }

        // -----------------------------------------------------
        // 表情情報（表情情報では、頂点の情報が必要になるので、
        // 頂点についていろいろやる前に初期化をやっておかねばならない）
        // -----------------------------------------------------
        private void InitializePMDFaces()
        {
            byte skin_idx = 0; // 通し番号
            int number_of_skin = 0;

            // 表情数
            foreach (MorphGroup mg in morph.Groups)
                number_of_skin += mg.Items.Count;
            pmd.skins = new PMD_Skin[number_of_skin];

            // 表情に関連するboneに影響を受ける頂点を数え上げる
            int numFaceVertices = CalcNumFaceVertices(fig.Tmo);

            // 表情
            foreach (MorphGroup mg in morph.Groups)
            {
                foreach (Morph m in mg.Items)
                {
                    pmd.skins[skin_idx] = new PMD_Skin();
                    pmd.skins[skin_idx].name = m.Name;
                    pmd.skins[skin_idx].vertices = new PMD_SkinVertex[numFaceVertices];

                    switch (mg.Name)
                    {
                        case "まゆ":
                            pmd.skins[skin_idx].panel_id = 1;
                            break;
                        case "目":
                            pmd.skins[skin_idx].panel_id = 2;
                            break;
                        case "リップ":
                            pmd.skins[skin_idx].panel_id = 3;
                            break;
                        case "その他":
                            pmd.skins[skin_idx].panel_id = 4;
                            break;
                    }

                    skin_idx++;
                }
            }
        }

        // -----------------------------------------------------
        // 予め、情報をコピーするmeshを選定し、並び替えておく
        // -----------------------------------------------------
        private void SelectMeshes()
        {
            meshes = new List<TSOSubMesh>();
            material_list = new T2PMaterialList(fig.TSOList, categories, TextureFilePrefix, UseSpheremap);

            int tso_num = 0;
            int sub_mesh_num = 0;
            foreach (TSOFile tso in fig.TSOList)
            {
                for (int script_num = 0; script_num < tso.sub_scripts.Length; script_num++)
                foreach (TSOMesh mesh in tso.meshes)
                foreach (TSOSubMesh sub_mesh in mesh.sub_meshes)
                {
                    if (sub_mesh.spec == script_num)
                    if (use_meshes[sub_mesh_num++] == true)
                    {
                        meshes.Add(sub_mesh);
                        material_list.Add(tso_num, script_num, UseEdge, tso.FileName);
                    }
                }
                tso_num++;
            }
        }

        private Matrix[] ClipBoneMatrices(TSOSubMesh sub_mesh, TMOFile tmo)
        {
            Matrix[] clipped_boneMatrices = new Matrix[sub_mesh.maxPalettes];
            for (int numPalettes = 0; numPalettes < sub_mesh.maxPalettes; numPalettes++)
            {
                TSONode tso_node = sub_mesh.GetBone(numPalettes);
                TMONode tmo_node = tmo.FindNodeByName(tso_node.Name);
                clipped_boneMatrices[numPalettes] = tso_node.offset_matrix * tmo_node.combined_matrix;
            }
            return clipped_boneMatrices;
        }

        List<int> inList_indices = new List<int>();

        // -----------------------------------------------------
        // 頂点を作成
        // -----------------------------------------------------
        private void MakePMDVertices(CorrespondTable cor_table, int mod_type)
        {
            List<PMD_Vertex> vertices = new List<PMD_Vertex>();
            List<int> indices = new List<int>();

            Dictionary<string, short> bone_name_idmap = new Dictionary<string, short>();
            {
                short i = 0;
                foreach (PMD_Bone node in pmd.nodes)
                {
                    bone_name_idmap[node.name] = i++;
                }
            }

            inList_indices.Clear();

            // -----------------------------------------------------
            // Tmoの変形を実行
            fig.TPOList.Transform();
            morph.Morph(fig.Tmo);
            fig.UpdateBoneMatricesWithoutTMOFrame();

            // -----------------------------------------------------
            // 情報をコピー
            int n_inList = -1; // list中のvertexの番号（処理の前に++するために、初期値は0でなく-1としている)
            int n_mesh = 0;
            int prevNumIndices = 0;
            int prevNumVertices = 0;

            foreach (TSOSubMesh sub_mesh in meshes)
            {
                int n_inMesh = -1; // mesh中のvertexの番号（処理の前に++するために、初期値は0でなく-1としている)
                int a=-1, b=-1, c=-1; // 隣合うインデックス

                Matrix[] clipped_boneMatrices = ClipBoneMatrices(sub_mesh, fig.Tmo);

                foreach (Vertex vertex in sub_mesh.vertices)
                {
                    n_inList++; // list中のvertexの番号を一つ増やす
                    n_inMesh++; // mesh中のvertexの番号を一つ増やす

                    // Tmo中のBoneに従って、Mesh中の頂点位置及び法線ベクトルを置き換えて、書き出す

                    Vector3 pos = Vector3.Empty;
                    Vector3 nor = Vector3.Empty;

                    foreach (SkinWeight sw in vertex.skin_weights)
                    {
                        Matrix m = clipped_boneMatrices[sw.bone_index];

                        // 頂点位置
                        pos += Vector3.TransformCoordinate(vertex.position, m) * sw.weight;

                        // 法線ベクトル
                        m.M41 = 0;
                        m.M42 = 0;
                        m.M43 = 0;
                        nor += Vector3.TransformCoordinate(vertex.normal, m) * sw.weight;
                    }

                    // -----------------------------------------------------
                    // 頂点情報をコピー
                    PMD_Vertex pmd_v = new PMD_Vertex();

                    pmd_v.position = Trans.CopyPos(pos);
                    pmd_v.normal = Trans.CopyPos(Vector3.Normalize(nor));
                    pmd_v.u = vertex.u;
                    pmd_v.v = vertex.v;

                    // -----------------------------------------------------
                    // スキニング
                    if (cor_table != null)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            TSONode tso_bone = sub_mesh.bones[vertex.skin_weights[i].bone_index];
                            string bone_name = cor_table.skinning[tso_bone.Name];
                            pmd_v.skin_weights[i].bone_index = bone_name_idmap[bone_name];
                            pmd_v.skin_weights[i].weight = vertex.skin_weights[i].weight;
                        }
                    }
                    else
                    {
                        pmd_v.skin_weights[0].bone_index = 0;
                        pmd_v.skin_weights[0].weight = 1.0f;
                    }

                    // -----------------------------------------------------
                    // 頂点リストに頂点を追加

                    // 重複している頂点がないかをチェックし、
                    // 存在すれば、そのインデックスを参照
                    // 存在しなければ、頂点リストに頂点を追加
                    int idx = -1;
                    for (int i = prevNumVertices; i < vertices.Count; i++)
                    {
                        if (vertices[i].position == pmd_v.position &&
                            vertices[i].normal == pmd_v.normal &&
                            vertices[i].u == pmd_v.u &&
                            vertices[i].v == pmd_v.v)
                        {
                            idx = i;
                            break;
                        }
                    }
                    if (idx == -1)
                    {
                        vertices.Add(pmd_v);
                        idx = vertices.Count - 1;
                        inList_indices.Add(idx);
                    }
                    else
                        inList_indices.Add(-1);

                    // -----------------------------------------------------
                    // 頂点インデックス

                    // 過去３つまでのインデックスを記憶しておく
                    a = b; b = c; c = idx;

                    // 隣合うインデックスが参照する頂点位置の重複を判定し、
                    // 重複している場合はインデックスの追加を省略する
                    if ((n_inMesh >= 2) &&
                        !(vertices[a].position == vertices[b].position ||
                          vertices[b].position == vertices[c].position ||
                          vertices[c].position == vertices[a].position))
                    {
                        if (n_inMesh % 2 == 0)
                        {
                            indices.Add(c);
                            indices.Add(b);
                            indices.Add(a);
                        }
                        else
                        {
                            indices.Add(a);
                            indices.Add(b);
                            indices.Add(c);
                        }
                    }

                }

                // meshごとのインデックス数を記録
                material_list.materials[n_mesh++].vindices_count = indices.Count - prevNumIndices;
                prevNumIndices = indices.Count;
                prevNumVertices = vertices.Count;
            }
            // -----------------------------------------------------
            // リストを配列に代入し直す
            // 頂点情報
            pmd.vertices = vertices.ToArray();
            // 頂点インデックス
            pmd.vindices = indices.ToArray();
            // マテリアル
            if (UniqueMaterial)
                material_list.UniqueMaterials();
            pmd.texture_file_names = material_list.GetTextureFileNameList();
            pmd.materials = material_list.materials.ToArray();
        }

        // 表情に関連するboneに影響を受ける頂点を数え上げる
        private int CalcNumFaceVertices(TMOFile tmo)
        {
            int n_vertex = 0; // 表情の頂点の番号（通し番号）
            int n_inList = -1; // list中のvertexの番号（処理の前に++するために、初期値は0でなく-1としている)
            foreach (TSOSubMesh sub_mesh in meshes)
            {
                int n_inMesh = -1; // mesh中のvertexの番号（処理の前に++するために、初期値は0でなく-1としている)
                foreach (Vertex vertex in sub_mesh.vertices)
                {
                    n_inList++; // list中のvertexの番号を一つ増やす
                    n_inMesh++; // mesh中のvertexの番号を一つ増やす
                    int idx = inList_indices[n_inList];

                    if (idx == -1)
                        continue;

                    PMD_Vertex pmd_v = pmd.vertices[idx];

                    // -----------------------------------------------------
                    // 表情情報

                    // 表情に関連するboneに影響を受ける頂点であれば、情報を記憶する
                    foreach (SkinWeight skin_w in vertex.skin_weights)
                    {
                        // 表情に関連するboneに影響を受ける頂点であれば、表情用の頂点とする
                        if (FACE_BONE_MIN <= sub_mesh.bone_indices[skin_w.bone_index]
                            && sub_mesh.bone_indices[skin_w.bone_index] <= FACE_BONE_MAX)
                        {
                            n_vertex++;
                            break;
                        }
                    }
                }
            }
            return n_vertex;
        }

        // 表情モーフを設定
        private void MakePMDFaces()
        {
            int n_vertex = 0; // 表情の頂点の番号（通し番号）
            int n_inList = -1; // list中のvertexの番号（処理の前に++するために、初期値は0でなく-1としている)
            // -----------------------------------------------------
            // 表情情報
            // -----------------------------------------------------
            List<Vector3[]> verPos_face = new List<Vector3[]>();

            foreach (TSOSubMesh sub_mesh in meshes)
            {
                int n_inMesh = -1; // mesh中のvertexの番号（処理の前に++するために、初期値は0でなく-1としている)
                verPos_face.Clear(); // 前回の分を消去

                foreach (MorphGroup mg in morph.Groups)
                {
                    foreach (Morph mi in mg.Items)
                    {
                        // 現在のモーフを有効にする
                        mi.Ratio = 1.0f;

                        // モーフ変形を実行
                        fig.TPOList.Transform();
                        morph.Morph(fig.Tmo);
                        fig.UpdateBoneMatricesWithoutTMOFrame();
                        
                        // 現在のモーフを無効にする
                        mi.Ratio = 0.0f;

                        Matrix[] clipped_boneMatrices_for_morphing = ClipBoneMatrices(sub_mesh, fig.Tmo);

                        // Tmo（各表情に対応する）中のBoneに従って、Mesh中の頂点位置を置き換える
                        Vector3[] output_v = new Vector3[sub_mesh.vertices.Length];
                        int n = 0;
                        foreach (Vertex vertex in sub_mesh.vertices)
                        {
                            Vector3 pos = Vector3.Empty;
                            Vector3 nor = Vector3.Empty;

                            foreach (SkinWeight sw in vertex.skin_weights)
                            {
                                // 頂点位置
                                Matrix m = clipped_boneMatrices_for_morphing[sw.bone_index];
                                pos += Vector3.TransformCoordinate(vertex.position, m) * sw.weight;
                            }

                            output_v[n++] = pos;
                        }
                        verPos_face.Add(output_v);
                    }

                    // モーフ変形を初期化する
                    fig.TPOList.Transform();
                    morph.Morph(fig.Tmo);
                    fig.UpdateBoneMatricesWithoutTMOFrame();
                }

                foreach (Vertex vertex in sub_mesh.vertices)
                {
                    n_inList++; // list中のvertexの番号を一つ増やす
                    n_inMesh++; // mesh中のvertexの番号を一つ増やす
                    int idx = inList_indices[n_inList];

                    if (idx == -1)
                        continue;

                    PMD_Vertex pmd_v = pmd.vertices[idx];

                    // 表情に関連するboneに影響を受ける頂点であれば、情報を記憶する
                    foreach (SkinWeight skin_w in vertex.skin_weights)
                    {
                        // 表情に関連するboneに影響を受ける頂点であれば、表情用の頂点とする
                        if (FACE_BONE_MIN <= sub_mesh.bone_indices[skin_w.bone_index]
                            && sub_mesh.bone_indices[skin_w.bone_index] <= FACE_BONE_MAX)
                        {
                            // 表情の頂点情報
                            for (int i = 0; i < pmd.skins.Length; i++)
                            {
                                pmd.skins[i].vertices[n_vertex] = new PMD_SkinVertex();

                                // 表情用の頂点の番号
                                pmd.skins[i].vertices[n_vertex].vertex_id = idx;

                                // 相対位置で指定
                                Vector3 pmd_face_pos = Trans.CopyPos(verPos_face[i][n_inMesh]);
                                pmd.skins[i].vertices[n_vertex].position = pmd_face_pos - pmd_v.position;
                            }

                            n_vertex++;
                            break;
                        }
                    }

                }
            }
        }

        // にっこりさせる
        public void NikkoriFace()
        {
            foreach (MorphGroup mg in morph.Groups)
            {
                if (mg.Name == "まゆ")
                {
                    foreach (Morph mi in mg.Items)
                    {
                        if (mi.Name == "にこり")
                        {
                            // 現在のモーフを有効にする
                            mi.Ratio = 1.0f;
                            break;
                        }
                    }
                }
                if (mg.Name == "目")
                {
                    foreach (Morph mi in mg.Items)
                    {
                        if (mi.Name == "笑い")
                        {
                            // 現在のモーフを有効にする
                            mi.Ratio = 1.0f;
                            break;
                        }
                    }
                }
                else if (mg.Name == "リップ")
                {
                    foreach (Morph mi in mg.Items)
                    {
                        if (mi.Name == "わーい")
                        {
                            // 現在のモーフを有効にする
                            mi.Ratio = 1.0f;
                            break;
                        }
                    }
                }
            }

            // モーフ変形を実行
            fig.TPOList.Transform();
            morph.Morph(fig.Tmo);
            fig.UpdateBoneMatricesWithoutTMOFrame();
        }

        // 初期の表情にする
        public void DefaultFace()
        {
            foreach (MorphGroup mg in morph.Groups)
            foreach (Morph mi in mg.Items)
                mi.Ratio = 0.0f;

            // モーフ変形を実行
            fig.TPOList.Transform();
            morph.Morph(fig.Tmo);
            fig.UpdateBoneMatricesWithoutTMOFrame();
        }
    }
}
