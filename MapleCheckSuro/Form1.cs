﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Google.Cloud.Vision.V1;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Presentation;
using System.Drawing.Imaging;
using System.IO;
using System.Collections;

namespace MapleCheckSuro
{
    public partial class Form1 : Form
    {
        private int clickCount1 = 0;
        private int clickCount2 = 0;

        private List<string> characterList = new List<string>();
        private List<string> scoreList = new List<string>();

        public Dictionary<string, string> heroDict = new Dictionary<string, string>();

        private CharacterForm characterForm;

        // 수정할때 임시로 받아둘 데이터
        private string subCharacter;
        private string subScore;
        private int listViewIdx;

        public Form1()
        {
            InitializeComponent();
        }

        private void imgBtn_Click(object sender, EventArgs e) // 캐릭터 이름 이미지 올리기
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                clickCount1++;

                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (clickCount1 == 1)
                    {
                        pictureBox1.Image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                    }
                    else if (clickCount1 == 2)
                    {
                        pictureBox2.Image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                    }
                    else if (clickCount1 == 3)
                    {
                        pictureBox3.Image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                        clickCount1 = 0;
                    }
                }
            }
        }

        private void imgBtn2_Click(object sender, EventArgs e) // 수로 점수 이미지 올리기
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                clickCount2++;

                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (clickCount2 == 1)
                    {
                        pictureBox4.Image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                    }
                    else if (clickCount2 == 2)
                    {
                        pictureBox5.Image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                    }
                    else if (clickCount2 == 3)
                    {
                        pictureBox6.Image = System.Drawing.Image.FromFile(openFileDialog.FileName);
                        clickCount2 = 0;
                    }
                }
            }
        }

        private void extractBtn_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null)
            {
                MessageBox.Show("Please load an image first.");
                return;
            }

            // "캐릭" : 캐릭터 이름 이미지, "점수" : 점수 이미지

            // 캐릭터 이미지 텍스트로 변환 후 리스트에 입력        
            var imagePath = SaveImageTemporarily(pictureBox1.Image);
            var text = ExtractTextFromImage(imagePath);
            if (!string.IsNullOrEmpty(text))
            {
                SaveToList("캐릭", text);
            }
            
            var imagePath2 = SaveImageTemporarily(pictureBox2.Image);
            var text2 = ExtractTextFromImage(imagePath2);
            SaveToList("캐릭", text2);

            var imagePath3 = SaveImageTemporarily(pictureBox3.Image);
            var text3 = ExtractTextFromImage(imagePath3);
            SaveToList("캐릭", text3);

            // 점수 이미지 텍스트로 변환 후 리스트에 입력
            var imagePath4 = SaveImageTemporarily(pictureBox4.Image);
            var text4 = ExtractTextFromImage(imagePath4);
            SaveToList("점수", text4);

            var imagePath5 = SaveImageTemporarily(pictureBox5.Image);
            var text5 = ExtractTextFromImage(imagePath5);
            SaveToList("점수", text5);
            
            var imagePath6 = SaveImageTemporarily(pictureBox6.Image);
            var text6 = ExtractTextFromImage(imagePath6);
            SaveToList("점수", text6);

            // 리스트 데이터 표시
            ShowListView();
        }

        private string SaveImageTemporarily(System.Drawing.Image image)
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tempImage.png");
            image.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
            return tempPath;
        }

        private void SaveToList(string gubun, string textData)
        {
            string[] linesArray = textData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            RemoveShortStrings(linesArray, 2); // 캐릭터명이 2글자 이상인것만 다시 걸러냄
            if (gubun == "캐릭")
            {
                characterList.AddRange(linesArray); // 기존 리스트에 값 추가
            }
            else if (gubun == "점수")
            {
                scoreList.AddRange(linesArray); // 기존 리스트에 값 추가
            }    
        }

        static string[] RemoveShortStrings(string[] array, int lengthThreshold)
        {
            return array.Where(str => str.Length > lengthThreshold).ToArray();
        }

        private void ShowListView()
        {
            if (characterList.Count > 0)
            {
                for (int i = 0; i < characterList.Count; i++)
                {
                    var character = characterList[i];
                    var score = scoreList[i];

                    var modifiedCharacter = "";

                    ListViewItem item = new ListViewItem((i + 1).ToString());

                    // 인식한 캐릭터명이 제대로 맞는지 확인, 틀리면 유사한 캐릭터명 입력
                    if (heroDict.ContainsKey(character))
                    {
                        item.SubItems.Add(character);
                        item.SubItems.Add(character); // 제대로 맞으니까 수정 없이 그대로 입력
                        item.SubItems.Add(score);

                        if (heroDict.Count > 0)
                        {
                            if (heroDict.ContainsKey(character))
                            {
                                item.SubItems.Add(heroDict[character]);
                            }
                            else
                            {
                                item.SubItems.Add("x");
                            }
                        }
                    }
                    else // 잘못 인식한 경우
                    {
                        item.SubItems.Add(character);

                        string[] existingTexts = heroDict.Keys.ToArray();                        
                        modifiedCharacter = StringMatcher.FindClosestMatch(existingTexts, character); // 레벤슈타인거리를 활용해 가장 유사한 캐릭터명을 가져옴
                        item.SubItems.Add(modifiedCharacter);
                        item.SubItems.Add(score);
                        if (heroDict.Count > 0)
                        {
                            if (heroDict.ContainsKey(modifiedCharacter))
                            {
                                item.SubItems.Add(heroDict[modifiedCharacter]);
                            }
                            else
                            {
                                item.SubItems.Add("x");
                            }
                        }
                    }
                    listView1.Items.Add(item);
                }
            }
        }

        private string ExtractTextFromImage(string imagePath) // 이미지 텍스트 추출
        {
            try
            {
                Credentials credentials = new Credentials
                {
                    type = "",
                    project_id = "",
                    private_key_id = "",
                    private_key = "",
                    client_email = "",
                    client_id = "",
                    auth_uri = "",
                    token_uri = "",
                    auth_provider_x509_cert_url = "",
                    client_x509_cert_url = "",
                };

                ImageAnnotatorClient client = new ImageAnnotatorClientBuilder
                {
                    JsonCredentials = JsonConvert.SerializeObject(credentials)
                }.Build();

                client = ImageAnnotatorClient.Create();
                var image = Google.Cloud.Vision.V1.Image.FromFile(imagePath);
                var response = client.DetectText(image);
                string extractedText = string.Empty;
                
                foreach (var annotation in response)
                {
                    if (annotation.Description != null)
                    {
                        extractedText += annotation.Description + "\n";
                    }
                    break;
                }
                return extractedText;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error during OCR processing: " + e.Message);
                return string.Empty;
            }
        }

        private void characterBtn_Click(object sender, EventArgs e)
        {
            if (characterForm == null || characterForm.IsDisposed)
            {
                characterForm = new CharacterForm(this); // Form2 인스턴스 생성
                characterForm.Show(); // Form2를 Modeless로 열기
            }
            else
            {
                characterForm.Focus(); // 이미 열려있는 경우 Form2에 포커스를 줌
            }
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            // ListView에서 선택된 아이템들의 값을 가져오기
            foreach (ListViewItem selectedItem in listView1.SelectedItems)
            {
                listViewIdx = selectedItem.Index;
                subCharacter = selectedItem.SubItems[2].Text;
                subScore = selectedItem.SubItems[3].Text;
                subChaTB.Text = subCharacter;
                scoreTB.Text = subScore;
            }
        }

        private void cellModifyBtn_Click(object sender, EventArgs e)
        {
            string modifyName = subChaTB.Text;
            string modifyScore = scoreTB.Text;

            listView1.Items[listViewIdx].SubItems[2].Text = modifyName;
            listView1.Items[listViewIdx].SubItems[3].Text = modifyScore;

            characterList[listViewIdx] = modifyName;
            scoreList[listViewIdx] = modifyScore;

            if (heroDict.Count > 0)
            {
                if (heroDict.ContainsKey(modifyName)) 
                {
                    listView1.Items[listViewIdx].SubItems[4].Text = heroDict[modifyName];
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string filePath = Path.Combine(myDocumentsPath, "꿈터달성캐릭.txt");

            try
            {
                // 파일에서 데이터 읽기
                string[] lines = File.ReadAllLines(filePath);

                // Dictionary 초기화
                heroDict.Clear();

                // 데이터를 Dictionary에 저장
                foreach (string line in lines)
                {
                    int delimiterIndex = line.IndexOf(" : ");
                    if (delimiterIndex != -1)
                    {
                        string mainCharacter = line.Substring(0, delimiterIndex).Trim();
                        string subCharactersPart = line.Substring(delimiterIndex + 3).Trim();
                        string[] subCharacters = subCharactersPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        // Dictionary에 추가
                        for (int i = 0; i < subCharacters.Length; i++)
                        {
                            heroDict.Add(subCharacters[i], mainCharacter);
                        }
                    }
                }
                // comboBox에 항목 추가
                AddUniqueValuesToComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show("불러올 본캐부캐목록이 없습니다. 먼저 입력해주세요.");
            }
        }

        private void AddUniqueValuesToComboBox()
        {
            // 중복을 제거할 HashSet 생성
            HashSet<string> uniqueValues = new HashSet<string>();

            // Dictionary의 값들 중복 없이 ComboBox에 추가
            foreach (var kvp in heroDict)
            {
                string value = kvp.Value;

                // HashSet을 사용하여 중복된 값 체크 후 추가
                if (!uniqueValues.Contains(value))
                {
                    comboBox1.Items.Add(value);
                    uniqueValues.Add(value);
                }
            }
        }

        private void cellDelBtn_Click(object sender, EventArgs e) // 불필요한 값 직접 삭제
        {
            characterList.RemoveAt(listViewIdx);
            scoreList.RemoveAt(listViewIdx);

            listView1.Items.RemoveAt(listViewIdx);
        }

        private void addBtn_Click(object sender, EventArgs e) // 직접 입력
        {
            string modifyName = subChaTB.Text;
            string modifyScore = scoreTB.Text;

            ListViewItem listViewItem = new ListViewItem((listView1.Items.Count+1).ToString());
            listViewItem.SubItems.Add(modifyName);
            listViewItem.SubItems.Add(modifyName);
            listViewItem.SubItems.Add(modifyScore);

            if (heroDict.Count > 0)
            {
                if (heroDict.ContainsKey(modifyName))
                {
                    listViewItem.SubItems.Add(heroDict[modifyName]);
                }
            }

            listView1.Items.Add(listViewItem);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            listView2.Items.Clear();

            scoreLb.Text = "점수 합 : ";
            solLb.Text = "조각 : ";

            int idx = 0;
            string mainCha = comboBox1.Text;
            int totalScore = 0;

            for (int i = 0; listView1.Items.Count > i; i++)
            {
                if (listView1.Items[i].SubItems[4].Text == mainCha)
                {
                    string subCha = listView1.Items[i].SubItems[2].Text;
                    string subScore = listView1.Items[i].SubItems[3].Text;

                    idx++;
                    ListViewItem item = new ListViewItem(idx.ToString());
                    item.SubItems.Add(subCha);
                    item.SubItems.Add(subScore);

                    listView2.Items.Add(item);

                    if (subScore.Length > 0)
                    {
                        string subScore2 = subScore.Replace(",", "");
                        totalScore += Convert.ToInt32(subScore2);
                    }
                    else
                    {
                        totalScore += 0;
                    }
                }
            }

            if (totalScore > 0)
            {
                scoreLb.Text = "점수 합 : " + totalScore.ToString();
                solLb.Text = "조각 : " + (totalScore / 1000).ToString();
            }
        }
    }
}
