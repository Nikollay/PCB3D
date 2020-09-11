using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Reflection;

using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
using SolidWorksTools;
using SolidWorksTools.File;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using ASM_3D;
using System.IO;

namespace PCB3D
{
    /// <summary>
    /// Summary description for PCB3D.
    /// </summary>
    [Guid("9abb7e56-8685-4884-93e1-ad38d7071dc2"), ComVisible(true)]
    [SwAddin(
        Description = "PCB3D description",
        Title = "PCB3D",
        LoadAtStartup = true
        )]
    public class SwAddin : ISwAddin
    {
        #region Local Variables
        ISldWorks iSwApp = null;
        ICommandManager iCmdMgr = null;
        int addinID = 0;
        BitmapHandler iBmp;

        public const int mainCmdGroupID = 5;
        public const int mainItemID1 = 0;
        public const int mainItemID2 = 1;
        public const int mainItemID3 = 2;
        public const int flyoutGroupID = 91;

        #region Event Handler Variables
        Hashtable openDocs = new Hashtable();
        SolidWorks.Interop.sldworks.SldWorks SwEventPtr = null;
        #endregion

        #region Property Manager Variables
        UserPMPage ppage = null;
        #endregion


        // Public Properties
        public ISldWorks SwApp
        {
            get { return iSwApp; }
        }
        public ICommandManager CmdMgr
        {
            get { return iCmdMgr; }
        }

        public Hashtable OpenDocs
        {
            get { return openDocs; }
        }

        #endregion

        #region SolidWorks Registration
        [ComRegisterFunctionAttribute]
        public static void RegisterFunction(Type t)
        {
            #region Get Custom Attribute: SwAddinAttribute
            SwAddinAttribute SWattr = null;
            Type type = typeof(SwAddin);

            foreach (System.Attribute attr in type.GetCustomAttributes(false))
            {
                if (attr is SwAddinAttribute)
                {
                    SWattr = attr as SwAddinAttribute;
                    break;
                }
            }

            #endregion

            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                Microsoft.Win32.RegistryKey addinkey = hklm.CreateSubKey(keyname);
                addinkey.SetValue(null, 0);

                addinkey.SetValue("Description", SWattr.Description);
                addinkey.SetValue("Title", SWattr.Title);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                addinkey = hkcu.CreateSubKey(keyname);
                addinkey.SetValue(null, Convert.ToInt32(SWattr.LoadAtStartup), Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (System.NullReferenceException nl)
            {
                Console.WriteLine("There was a problem registering this dll: SWattr is null. \n\"" + nl.Message + "\"");
                System.Windows.Forms.MessageBox.Show("There was a problem registering this dll: SWattr is null.\n\"" + nl.Message + "\"");
            }

            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);

                System.Windows.Forms.MessageBox.Show("There was a problem registering the function: \n\"" + e.Message + "\"");
            }
        }

        [ComUnregisterFunctionAttribute]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                hklm.DeleteSubKey(keyname);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                hkcu.DeleteSubKey(keyname);
            }
            catch (System.NullReferenceException nl)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + nl.Message);
                System.Windows.Forms.MessageBox.Show("There was a problem unregistering this dll: \n\"" + nl.Message + "\"");
            }
            catch (System.Exception e)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + e.Message);
                System.Windows.Forms.MessageBox.Show("There was a problem unregistering this dll: \n\"" + e.Message + "\"");
            }
        }

        #endregion

        #region ISwAddin Implementation
        public SwAddin()
        {
        }

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            iSwApp = (ISldWorks)ThisSW;
            addinID = cookie;

            //Setup callbacks
            iSwApp.SetAddinCallbackInfo(0, this, addinID);

            #region Setup the Command Manager
            iCmdMgr = iSwApp.GetCommandManager(cookie);
            AddCommandMgr();
            #endregion

            #region Setup the Event Handlers
            SwEventPtr = (SolidWorks.Interop.sldworks.SldWorks)iSwApp;
            openDocs = new Hashtable();
            AttachEventHandlers();
            #endregion

            #region Setup Sample Property Manager
            AddPMP();
            #endregion

            return true;
        }

        public bool DisconnectFromSW()
        {
            RemoveCommandMgr();
            RemovePMP();
            DetachEventHandlers();

            System.Runtime.InteropServices.Marshal.ReleaseComObject(iCmdMgr);
            iCmdMgr = null;
            System.Runtime.InteropServices.Marshal.ReleaseComObject(iSwApp);
            iSwApp = null;
            //The addin _must_ call GC.Collect() here in order to retrieve all managed code pointers 
            GC.Collect();
            GC.WaitForPendingFinalizers();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }
        #endregion

        #region UI Methods
        public void AddCommandMgr()
        {
            ICommandGroup cmdGroup;
            if (iBmp == null)
                iBmp = new BitmapHandler();
            Assembly thisAssembly;
            int cmdIndex0, cmdIndex1;
            string Title = "C# Addin", ToolTip = "C# Addin";


            int[] docTypes = new int[]{(int)swDocumentTypes_e.swDocASSEMBLY,
                                       (int)swDocumentTypes_e.swDocDRAWING,
                                       (int)swDocumentTypes_e.swDocPART};

            thisAssembly = System.Reflection.Assembly.GetAssembly(this.GetType());


            int cmdGroupErr = 0;
            bool ignorePrevious = false;

            object registryIDs;
            //get the ID information stored in the registry
            bool getDataResult = iCmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out registryIDs);

            int[] knownIDs = new int[2] { mainItemID1, mainItemID2 };

            if (getDataResult)
            {
                if (!CompareIDs((int[])registryIDs, knownIDs)) //if the IDs don't match, reset the commandGroup
                {
                    ignorePrevious = true;
                }
            }

            cmdGroup = iCmdMgr.CreateCommandGroup2(mainCmdGroupID, Title, ToolTip, "", -1, ignorePrevious, ref cmdGroupErr);
            cmdGroup.LargeIconList = iBmp.CreateFileFromResourceBitmap("PCB3D.ToolbarLarge.bmp", thisAssembly);
            cmdGroup.SmallIconList = iBmp.CreateFileFromResourceBitmap("PCB3D.ToolbarSmall.bmp", thisAssembly);
            cmdGroup.LargeMainIcon = iBmp.CreateFileFromResourceBitmap("PCB3D.MainIconLarge.bmp", thisAssembly);
            cmdGroup.SmallMainIcon = iBmp.CreateFileFromResourceBitmap("PCB3D.MainIconSmall.bmp", thisAssembly);

            int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);
            cmdIndex0 = cmdGroup.AddCommandItem2("Create3D", -1, "Create a 3D", "Create 3D", 0, "Create3D", "", mainItemID1, menuToolbarOption);
            //cmdIndex1 = cmdGroup.AddCommandItem2("Show PMP", -1, "Display sample property manager", "Show PMP", 2, "ShowPMP", "EnablePMP", mainItemID2, menuToolbarOption);

            cmdGroup.HasToolbar = true;
            cmdGroup.HasMenu = true;
            cmdGroup.Activate();

            bool bResult;



            FlyoutGroup flyGroup = iCmdMgr.CreateFlyoutGroup(flyoutGroupID, "Dynamic Flyout", "Flyout Tooltip", "Flyout Hint",
              cmdGroup.SmallMainIcon, cmdGroup.LargeMainIcon, cmdGroup.SmallIconList, cmdGroup.LargeIconList, "FlyoutCallback", "FlyoutEnable");


            flyGroup.AddCommandItem("FlyoutCommand 1", "test", 0, "FlyoutCommandItem1", "FlyoutEnableCommandItem1");

            flyGroup.FlyoutType = (int)swCommandFlyoutStyle_e.swCommandFlyoutStyle_Simple;


            foreach (int type in docTypes)
            {
                CommandTab cmdTab;

                cmdTab = iCmdMgr.GetCommandTab(type, Title);

                if (cmdTab != null & !getDataResult | ignorePrevious)//if tab exists, but we have ignored the registry info (or changed command group ID), re-create the tab.  Otherwise the ids won't matchup and the tab will be blank
                {
                    bool res = iCmdMgr.RemoveCommandTab(cmdTab);
                    cmdTab = null;
                }

                //if cmdTab is null, must be first load (possibly after reset), add the commands to the tabs
                if (cmdTab == null)
                {
                    cmdTab = iCmdMgr.AddCommandTab(type, Title);

                    CommandTabBox cmdBox = cmdTab.AddCommandTabBox();

                    int[] cmdIDs = new int[2];
                    int[] TextType = new int[2];

                    cmdIDs[0] = cmdGroup.get_CommandID(cmdIndex0);

                    TextType[0] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

                    //cmdIDs[1] = cmdGroup.get_CommandID(cmdIndex1);

                    //TextType[1] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

                    cmdIDs[1] = cmdGroup.ToolbarId;

                    TextType[1] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal | (int)swCommandTabButtonFlyoutStyle_e.swCommandTabButton_ActionFlyout;

                    bResult = cmdBox.AddCommands(cmdIDs, TextType);



                    CommandTabBox cmdBox1 = cmdTab.AddCommandTabBox();
                    cmdIDs = new int[1];
                    TextType = new int[1];

                    cmdIDs[0] = flyGroup.CmdID;
                    TextType[0] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow | (int)swCommandTabButtonFlyoutStyle_e.swCommandTabButton_ActionFlyout;

                    bResult = cmdBox1.AddCommands(cmdIDs, TextType);

                    cmdTab.AddSeparator(cmdBox1, cmdIDs[0]);

                }

            }
            thisAssembly = null;

        }

        public void RemoveCommandMgr()
        {
            iBmp.Dispose();

            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);
            iCmdMgr.RemoveFlyoutGroup(flyoutGroupID);
        }

        public bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            List<int> storedList = new List<int>(storedIDs);
            List<int> addinList = new List<int>(addinIDs);

            addinList.Sort();
            storedList.Sort();

            if (addinList.Count != storedList.Count)
            {
                return false;
            }
            else
            {

                for (int i = 0; i < addinList.Count; i++)
                {
                    if (addinList[i] != storedList[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public Boolean AddPMP()
        {
            ppage = new UserPMPage(this);
            return true;
        }

        public Boolean RemovePMP()
        {
            ppage = null;
            return true;
        }

        #endregion

        #region UI Callbacks
        public void Create3D()
        {

            Board board;
            string filename;


            ////SolidWorks
            //Console.WriteLine("Подключение к SldWorks.Application");
            //var progId = "SldWorks.Application.27";
            //var progType = System.Type.GetTypeFromProgID(progId);
            //var swApp = System.Activator.CreateInstance(progType) as ISldWorks;
            //swApp.Visible = true;
            //Console.WriteLine("Успешное подключение к версии SldWorks.Application " + swApp.RevisionNumber());
            //Console.WriteLine(DateTime.Now.ToString());
            //Console.CursorSize = 100;

            filename = iSwApp.GetOpenFileName("Открыть файл", "", "xml Files (*.xml)|*.xml|", out _, out _, out _); //Board.GetFilename();
            if (String.IsNullOrWhiteSpace(filename)) { return; }//System.Environment.Exit(0);
            board = Board.GetfromXML(filename);

            ModelDoc2 swModel;
            AssemblyDoc swAssy;
            ModelView activeModelView;

            //Новая сборка платы
            double swSheetWidth = 0, swSheetHeight = 0;
            string boardName;
            int Errors = 0, Warnings = 0;
            swAssy = (AssemblyDoc)iSwApp.NewDocument("D:\\PDM\\EPDM_LIBRARY\\EPDM_SolidWorks\\EPDM_SWR_Templates\\Модуль_печатной_платы.asmdot", (int)swDwgPaperSizes_e.swDwgPaperA2size, swSheetWidth, swSheetHeight);
            swModel = (ModelDoc2)swAssy;
            //Сохранение
            boardName = filename.Remove(filename.Length - 3) + "SLDASM";
            Console.WriteLine(boardName);
            swModel.Extension.SaveAs(boardName, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_UpdateInactiveViews, null, ref Errors, ref Warnings);
            //**********

            //Доска
            Component2 board_body;
            PartDoc part;
            ModelDoc2 swCompModel;
            Feature swRefPlaneFeat, plane;
            swAssy.InsertNewVirtualPart(null, out board_body);
            board_body.Select4(false, null, false);
            swAssy.EditPart();
            swCompModel = (ModelDoc2)board_body.GetModelDoc2();
            part = (PartDoc)swCompModel;
            part.SetMaterialPropertyName2("-00", "гост материалы.sldmat", "Rogers 4003C");

            int j = 1;
            do
            {
                swRefPlaneFeat = (Feature)swCompModel.FeatureByPositionReverse(j);
                j = j + 1;
            }
            while (swRefPlaneFeat.Name != "Спереди");

            plane = (Feature)board_body.GetCorresponding(swRefPlaneFeat);
            plane.Select2(false, -1);

            swModel.SketchManager.InsertSketch(false);
            swModel.SketchManager.AddToDB = true;

            //Эскизы
            foreach (Object skt in board.sketh)
            {
                if (skt.GetType().FullName == "ASM_3D.Line") { Line sk = (Line)skt; swModel.SketchManager.CreateLine(sk.x1, sk.y1, 0, sk.x2, sk.y2, 0); }
                if (skt.GetType().FullName == "ASM_3D.Arc") { Arc sk = (Arc)skt; swModel.SketchManager.CreateArc(sk.xc, sk.yc, 0, sk.x1, sk.y1, 0, sk.x2, sk.y2, 0, sk.direction); }
            }
            swModel.FeatureManager.FeatureExtrusion3(true, false, false, 0, 0, board.thickness, board.thickness, false, false, false, false, 0, 0, false, false, false, false, true, true, true, 0, 0, false);
            swModel.ClearSelection2(true);

            plane.Select2(false, -1);
            swModel.SketchManager.InsertSketch(false);
            swModel.SketchManager.AddToDB = true;
            swModel.SketchManager.DisplayWhenAdded = false;

            foreach (Circle c in board.circles)
            {
                swModel.SketchManager.CreateCircleByRadius(c.xc, c.yc, 0, c.radius);
            }
            swModel.FeatureManager.FeatureCut3(true, false, true, 1, 0, board.thickness, board.thickness, false, false, false, false, 1.74532925199433E-02, 1.74532925199433E-02, false, false, false, false, false, true, true, true, true, false, 0, 0, false);

            swAssy.HideComponent();
            swAssy.ShowComponent();
            swModel.ClearSelection2(true);
            swAssy.EditAssembly();

            List<string> allFoundFiles = new List<string>(System.IO.Directory.GetFiles("D:\\PDM\\Прочие изделия\\ЭРИ", "*.*", SearchOption.AllDirectories));
            Dictionary<string, string> empty = new Dictionary<string, string>();

            foreach (ASM_3D.Component comp in board.components)
            {
                comp.fileName = allFoundFiles.Find(item => item.Contains(comp.part_Number));
                if (String.IsNullOrWhiteSpace(comp.fileName))
                {
                    comp.fileName = "D:\\PDM\\Прочие изделия\\ЭРИ\\Zero.SLDPRT";
                    if (!empty.ContainsKey(comp.part_Number)) { empty.Add(comp.part_Number, comp.part_Number); }
                }
            }

            double[] transforms, dMatrix;
            string[] coordSys, names;
            double alfa, beta, gamma, x, y, z;
            names = new string[board.components.Count];
            coordSys = new string[board.components.Count];
            dMatrix = new double[16];
            transforms = new double[board.components.Count * 16];

            for (int i = 0; i < board.components.Count; i++)
            {
                names[i] = board.components[i].fileName;
            }
            int n = 0;
            foreach (ASM_3D.Component comp in board.components)
            {
                alfa = 0;
                x = comp.x;
                y = comp.y;
                if (comp.layer == 1) //Если Top
                {
                    z = (comp.z + comp.standOff);
                    beta = -Math.PI / 2;
                }
                else             //Иначе Bottom
                {
                    z = (comp.z - comp.standOff) / 1000;
                    beta = Math.PI / 2;
                }
                gamma = -(comp.rotation / 180) * Math.PI;

                dMatrix[0] = Math.Cos(alfa) * Math.Cos(gamma) - Math.Sin(alfa) * Math.Cos(beta) * Math.Sin(gamma);
                dMatrix[1] = -Math.Cos(alfa) * Math.Sin(gamma) - Math.Sin(alfa) * Math.Cos(beta) * Math.Cos(gamma);
                dMatrix[2] = Math.Sin(alfa) * Math.Sin(beta); //1 строка матрицы вращения
                dMatrix[3] = Math.Sin(alfa) * Math.Cos(gamma) + Math.Cos(alfa) * Math.Cos(beta) * Math.Sin(gamma);
                dMatrix[4] = -Math.Sin(alfa) * Math.Sin(gamma) + Math.Cos(alfa) * Math.Cos(beta) * Math.Cos(gamma);
                dMatrix[5] = -Math.Cos(alfa) * Math.Sin(beta); //2 строка матрицы вращения
                dMatrix[6] = Math.Sin(beta) * Math.Sin(gamma);
                dMatrix[7] = Math.Sin(beta) * Math.Cos(gamma);
                dMatrix[8] = Math.Cos(beta); //3 строка матрицы вращения
                dMatrix[9] = x; dMatrix[10] = y; dMatrix[11] = z; //Координаты
                dMatrix[12] = 1; //Масштаб
                dMatrix[13] = 0; dMatrix[14] = 0; dMatrix[15] = 0; //Ничего

                for (int k = 0; k < dMatrix.Length; k++) { transforms[n * 16 + k] = dMatrix[k]; }
                n++;
            }

            //Вставка
            swAssy.AddComponents3(names, transforms, coordSys);

            //Фиксация
            swModel.Extension.SelectAll();
            swAssy.FixComponent();
            swModel.ClearSelection2(true);

            activeModelView = (ModelView)swModel.ActiveView;
            activeModelView.DisplayMode = (int)swViewDisplayMode_e.swViewDisplayMode_ShadedWithEdges;
            //****************************

            UserProgressBar pb;
            iSwApp.GetUserProgressBar(out pb);

            //Заполнение поз. обозначений
            List<Component2> compsColl = new List<Component2>(); //Коллекция из компонентов сборки платы
            Feature swFeat;
            Component2 compTemp;
            pb.Start(0, board.components.Count, "Поиск");
            int itm = 0;
            swFeat = (Feature)swModel.FirstFeature();
            while (swFeat != null)
            {
                pb.UpdateProgress(itm);
                //pb.UpdateTitle(itm);
                if (swFeat.GetTypeName().Equals("Reference")) //Заполняем коллекцию изделиями
                {
                    compTemp = (Component2)swFeat.GetSpecificFeature2();
                    compsColl.Add(compTemp);
                }
                swFeat = (Feature)swFeat.GetNextFeature();
                itm++;
            }
            pb.End();

            compsColl[0].Name2 = "Плата"; //Пререименовываем деталь      
            if (compsColl.Count - 1 == board.components.Count) //Проверка чтобы не сбились поз. обозначения, если появятся значит все правильно иначе они не нужны
            {
                for (int i = 0; i < board.components.Count; i++)
                    compsColl[i + 1].ComponentReference = board.components[i].physicalDesignator; //Заполняем поз. обозначениями
            }

            string estr = "";
            if (empty.Count != 0)
            {
                foreach (KeyValuePair<string, string> str in empty) { estr = estr + str.Value + System.Environment.NewLine; }
                MessageBox.Show(estr, "Не найдены");
                //swApp.SendMsgToUser2("Не найдены" + estr, 2, 2);
            }
            //**************
            //swApp.ExitApp();
            //swApp = null;
        }
        public void CreateCube()
        {
            //make sure we have a part open
            string partTemplate = iSwApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            if ((partTemplate != null) && (partTemplate != ""))
            {
                IModelDoc2 modDoc = (IModelDoc2)iSwApp.NewDocument(partTemplate, (int)swDwgPaperSizes_e.swDwgPaperA2size, 0.0, 0.0);

                modDoc.InsertSketch2(true);
                modDoc.SketchRectangle(0, 0, 0, .1, .1, .1, false);
                //Extrude the sketch
                IFeatureManager featMan = modDoc.FeatureManager;
                featMan.FeatureExtrusion(true,
                    false, false,
                    (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                    0.1, 0.0,
                    false, false,
                    false, false,
                    0.0, 0.0,
                    false, false,
                    false, false,
                    true,
                    false, false);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("There is no part template available. Please check your options and make sure there is a part template selected, or select a new part template.");
            }
        }


        public void ShowPMP()
        {
            if (ppage != null)
                ppage.Show();
        }

        public int EnablePMP()
        {
            if (iSwApp.ActiveDoc != null)
                return 1;
            else
                return 0;
        }

        public void FlyoutCallback()
        {
            FlyoutGroup flyGroup = iCmdMgr.GetFlyoutGroup(flyoutGroupID);
            flyGroup.RemoveAllCommandItems();

            flyGroup.AddCommandItem(System.DateTime.Now.ToLongTimeString(), "test", 0, "FlyoutCommandItem1", "FlyoutEnableCommandItem1");

        }
        public int FlyoutEnable()
        {
            return 1;
        }

        public void FlyoutCommandItem1()
        {
            iSwApp.SendMsgToUser("Flyout command 1");
        }

        public int FlyoutEnableCommandItem1()
        {
            return 1;
        }
        #endregion

        #region Event Methods
        public bool AttachEventHandlers()
        {
            AttachSwEvents();
            //Listen for events on all currently open docs
            AttachEventsToAllDocuments();
            return true;
        }

        private bool AttachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify += new DSldWorksEvents_ActiveDocChangeNotifyEventHandler(OnDocChange);
                SwEventPtr.DocumentLoadNotify2 += new DSldWorksEvents_DocumentLoadNotify2EventHandler(OnDocLoad);
                SwEventPtr.FileNewNotify2 += new DSldWorksEvents_FileNewNotify2EventHandler(OnFileNew);
                SwEventPtr.ActiveModelDocChangeNotify += new DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler(OnModelChange);
                SwEventPtr.FileOpenPostNotify += new DSldWorksEvents_FileOpenPostNotifyEventHandler(FileOpenPostNotify);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }



        private bool DetachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify -= new DSldWorksEvents_ActiveDocChangeNotifyEventHandler(OnDocChange);
                SwEventPtr.DocumentLoadNotify2 -= new DSldWorksEvents_DocumentLoadNotify2EventHandler(OnDocLoad);
                SwEventPtr.FileNewNotify2 -= new DSldWorksEvents_FileNewNotify2EventHandler(OnFileNew);
                SwEventPtr.ActiveModelDocChangeNotify -= new DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler(OnModelChange);
                SwEventPtr.FileOpenPostNotify -= new DSldWorksEvents_FileOpenPostNotifyEventHandler(FileOpenPostNotify);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

        }

        public void AttachEventsToAllDocuments()
        {
            ModelDoc2 modDoc = (ModelDoc2)iSwApp.GetFirstDocument();
            while (modDoc != null)
            {
                if (!openDocs.Contains(modDoc))
                {
                    AttachModelDocEventHandler(modDoc);
                }
                modDoc = (ModelDoc2)modDoc.GetNext();
            }
        }

        public bool AttachModelDocEventHandler(ModelDoc2 modDoc)
        {
            if (modDoc == null)
                return false;

            DocumentEventHandler docHandler = null;

            if (!openDocs.Contains(modDoc))
            {
                switch (modDoc.GetType())
                {
                    case (int)swDocumentTypes_e.swDocPART:
                        {
                            docHandler = new PartEventHandler(modDoc, this);
                            break;
                        }
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                        {
                            docHandler = new AssemblyEventHandler(modDoc, this);
                            break;
                        }
                    case (int)swDocumentTypes_e.swDocDRAWING:
                        {
                            docHandler = new DrawingEventHandler(modDoc, this);
                            break;
                        }
                    default:
                        {
                            return false; //Unsupported document type
                        }
                }
                docHandler.AttachEventHandlers();
                openDocs.Add(modDoc, docHandler);
            }
            return true;
        }

        public bool DetachModelEventHandler(ModelDoc2 modDoc)
        {
            DocumentEventHandler docHandler;
            docHandler = (DocumentEventHandler)openDocs[modDoc];
            openDocs.Remove(modDoc);
            modDoc = null;
            docHandler = null;
            return true;
        }

        public bool DetachEventHandlers()
        {
            DetachSwEvents();

            //Close events on all currently open docs
            DocumentEventHandler docHandler;
            int numKeys = openDocs.Count;
            object[] keys = new Object[numKeys];

            //Remove all document event handlers
            openDocs.Keys.CopyTo(keys, 0);
            foreach (ModelDoc2 key in keys)
            {
                docHandler = (DocumentEventHandler)openDocs[key];
                docHandler.DetachEventHandlers(); //This also removes the pair from the hash
                docHandler = null;
            }
            return true;
        }
        #endregion

        #region Event Handlers
        //Events
        public int OnDocChange()
        {
            return 0;
        }

        public int OnDocLoad(string docTitle, string docPath)
        {
            return 0;
        }

        int FileOpenPostNotify(string FileName)
        {
            AttachEventsToAllDocuments();
            return 0;
        }

        public int OnFileNew(object newDoc, int docType, string templateName)
        {
            AttachEventsToAllDocuments();
            return 0;
        }

        public int OnModelChange()
        {
            return 0;
        }

        #endregion
    }

}
