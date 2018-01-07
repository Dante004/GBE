using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GBESharp
{
    public partial class Form1 : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct PeekMsg
        {
            public IntPtr hWnd;
            public Message msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public Point p;
        }

        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern bool PeekMessage(out PeekMsg msg, IntPtr hWnd,
                 uint messageFilterMin, uint messageFilterMax, uint flags);

        const int frames_per_second = 60;
        int MAX_FRAMES_SKIPPED = 2;
        int width = 160 * 2;
        int height = 144 * 2;
        public long frequency = Stopwatch.Frequency;
        public long ticks_per_frame = Stopwatch.Frequency / frames_per_second;
        Bitmap bitmap;
        public Graphics g;
        public Stopwatch stopWatch = new Stopwatch();
        public long nextFrameStart;
        Z80 z80;
        GPU gpu;
        Memory memory;
        Rectangle rectangle;
        double scanLineTicks;
        uint[] pixels = new uint[160 * 144];
        Game game;



        public Form1()
        {
            InitializeComponent();
            InitFrame();
        }

        void OnApplicationIdle(object sender, EventArgs e)
        {
            if (z80 == null || !Focused)
            {
                return;
            }
            PeekMsg msg;
            while (!PeekMessage(out msg, IntPtr.Zero, 0, 0, 0))
            {
                int updates = 0;
                bool updateBitmap = true;
                do
                {
                    UpdateModel(updateBitmap);
                    updateBitmap = false;
                    nextFrameStart += ticks_per_frame;

                } while (nextFrameStart < stopWatch.ElapsedTicks && ++updates < MAX_FRAMES_SKIPPED);
                RenderFrame();
                long remainigTicks = nextFrameStart - stopWatch.ElapsedTicks;
                if (remainigTicks > 0)
                {
                    Thread.Sleep((int)(1000 * remainigTicks / frequency));
                }
                else if (updates == MAX_FRAMES_SKIPPED)
                {
                    nextFrameStart = stopWatch.ElapsedTicks;
                }
            }
        }

        void UpdateModel(bool updateBitmap)
        {
            if (updateBitmap)
            {
                uint[] backgroundPalette = GPU.backgroundPalette;
                uint[] objectPalette0 = GPU.objectPallete0;
                uint[] objectPalette1 = GPU.objectPallete1;
                uint[,] backgroundBuffer = gpu.backgroundBuffer;
                uint[,] windowBuffer = gpu.windowBuffer;
                byte[] oam = memory.oam;

                for (int y = 0, pixelIndex = 0; y < 144; y++)
                {
                    GPU.ly = y;
                    GPU.lcdcMode = LcdcModeType.SearchingOamRam;
                    if (Z80.lcdcInterruptEnabled && (GPU.lcdcOamInterruptEnabled || (GPU.lcdcLycLyCoincidenceInterruptEnabled && GPU.lyCompare == y)))
                    {
                        Z80.lcdcInterruptRequested = true;
                    }
                    ExecuteProcessor(800);
                    GPU.lcdcMode = LcdcModeType.TransferingData;
                    ExecuteProcessor(1720);

                    gpu.UpdateWindow();
                    gpu.UpdateBackground();
                    gpu.UpdateSpriteTiles();

                    bool backgroundDisplayed = GPU.backgroundDisplayed;
                    bool backgroundAndWindowTileDataSelect = GPU.backgroundAndWindowTileDataSelect;
                    bool backgroundTileMapDisplaySelect = GPU.backgroundTileMapDisplaySelect;
                    int scrollX = GPU.scrollX;
                    int scrollY = GPU.scrollY;
                    bool windowDisplayed = GPU.windowDisplayed;
                    bool windowTileMapDisplaySelect = GPU.windowTileMapDisplaySelect;
                    int windowX = GPU.windowX - 7;
                    int windowY = GPU.windowY;

                    int windowPointY = windowY + y;

                    for (int x = 0; x < 160; x++, pixelIndex++)
                    {

                        uint intensity = 0;

                        if (backgroundDisplayed)
                        {
                            intensity = backgroundBuffer[0xFF & (scrollY + y), 0xFF & (scrollX + x)];
                        }

                        if (windowDisplayed && y >= windowY && y < windowY + 144 && x >= windowX && x < windowX + 160 && windowX >= -7 && windowX <= 159 && windowY >= 0 && windowY <= 143)
                        {
                            intensity = windowBuffer[y - windowY, x - windowX];
                        }

                        pixels[pixelIndex] = intensity;
                    }

                    if (GPU.spritesDisplayed)
                    {
                        uint[,,,] spriteTile = gpu.spriteTile;
                        if (GPU.largeSprites)
                        {
                            for (int address = 0; address < 160; address += 4)
                            {
                                int spriteY = oam[address];
                                int spriteX = oam[address + 1];
                                if (spriteY == 0 || spriteX == 0 || spriteY >= 160 || spriteX >= 168)
                                {
                                    continue;
                                }
                                spriteY -= 16;
                                if (spriteY > y || spriteY + 15 < y)
                                {
                                    continue;
                                }
                                spriteX -= 8;

                                int spriteTileIndex0 = 0xFE & oam[address + 2];
                                int spriteTileIndex1 = spriteTileIndex0 | 0x01;
                                int spriteFlags = oam[address + 3];
                                bool spritePriority = (0x80 & spriteFlags) == 0x80;
                                bool spriteYFlipped = (0x40 & spriteFlags) == 0x40;
                                bool spriteXFlipped = (0x20 & spriteFlags) == 0x20;
                                int spritePalette = (0x10 & spriteFlags) == 0x10 ? 1 : 0;

                                if (spriteYFlipped)
                                {
                                    int temp = spriteTileIndex0;
                                    spriteTileIndex0 = spriteTileIndex1;
                                    spriteTileIndex1 = temp;
                                }

                                int spriteRow = y - spriteY;
                                if (spriteRow >= 0 && spriteRow < 8)
                                {
                                    int screenAddress = (y << 7) + (y << 5) + spriteX;
                                    for (int x = 0; x < 8; x++, screenAddress++)
                                    {
                                        int screenX = spriteX + x;
                                        if (screenX >= 0 && screenX < 160)
                                        {
                                            uint color = spriteTile[spriteTileIndex0,
                                                spriteYFlipped ? 7 - spriteRow : spriteRow,
                                                spriteXFlipped ? 7 - x : x, spritePalette];
                                            if (color > 0)
                                            {
                                                if (spritePriority)
                                                {
                                                    if (pixels[screenAddress] == 0xFFFFFFFF)
                                                    {
                                                        pixels[screenAddress] = color;
                                                    }
                                                }
                                                else
                                                {
                                                    pixels[screenAddress] = color;
                                                }
                                            }
                                        }
                                    }
                                    continue;
                                }

                                spriteY += 8;

                                spriteRow = y - spriteY;
                                if (spriteRow >= 0 && spriteRow < 8)
                                {
                                    int screenAddress = (y << 7) + (y << 5) + spriteX;
                                    for (int x = 0; x < 8; x++, screenAddress++)
                                    {
                                        int screenX = spriteX + x;
                                        if (screenX >= 0 && screenX < 160)
                                        {
                                            uint color = spriteTile[spriteTileIndex1,
                                                spriteYFlipped ? 7 - spriteRow : spriteRow,
                                                spriteXFlipped ? 7 - x : x, spritePalette];
                                            if (color > 0)
                                            {
                                                if (spritePriority)
                                                {
                                                    if (pixels[screenAddress] == 0xFFFFFFFF)
                                                    {
                                                        pixels[screenAddress] = color;
                                                    }
                                                }
                                                else
                                                {
                                                    pixels[screenAddress] = color;
                                                }
                                            }
                                        }
                                    }
                                }

                            }

                        }
                        else
                        {
                            for (int address = 0; address < 160; address += 4)
                            {
                                int spriteY = oam[address];
                                int spriteX = oam[address + 1];
                                if (spriteY == 0 || spriteX == 0 || spriteY >= 160 || spriteX >= 168)
                                {
                                    continue;
                                }
                                spriteY -= 16;
                                if (spriteY > y || spriteY + 7 < y)
                                {
                                    continue;
                                }
                                spriteX -= 8;

                                int spriteTileIndex = oam[address + 2];
                                int spriteFlags = oam[address + 3];
                                bool spritePriority = (0x80 & spriteFlags) == 0x80;
                                bool spriteYFlipped = (0x40 & spriteFlags) == 0x40;
                                bool spriteXFlipped = (0x20 & spriteFlags) == 0x20;
                                int spritePalette = (0x10 & spriteFlags) == 0x10 ? 1 : 0;

                                int spriteRow = y - spriteY;
                                int screenAddress = (y << 7) + (y << 5) + spriteX;
                                for (int x = 0; x < 8; x++, screenAddress++)
                                {
                                    int screenX = spriteX + x;
                                    if (screenX >= 0 && screenX < 160)
                                    {
                                        uint color = spriteTile[spriteTileIndex,
                                            spriteYFlipped ? 7 - spriteRow : spriteRow,
                                            spriteXFlipped ? 7 - x : x, spritePalette];
                                        if (color > 0)
                                        {
                                            if (spritePriority)
                                            {
                                                if (pixels[screenAddress] == 0xFFFFFFFF)
                                                {
                                                    pixels[screenAddress] = color;
                                                }
                                            }
                                            else
                                            {
                                                pixels[screenAddress] = color;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    GPU.lcdcMode = LcdcModeType.HBlank;
                    if (Z80.lcdcInterruptEnabled && GPU.lcdcHBlankInterruptEnabled)
                    {
                        Z80.lcdcInterruptRequested = true;
                    }
                    ExecuteProcessor(2040);
                    AddTicksPerScanLine();

                }
            }
            else
            {
                for (int y = 0; y < 144; y++)
                {

                    GPU.ly = y;
                    GPU.lcdcMode = LcdcModeType.SearchingOamRam;
                    if (Z80.lcdcInterruptEnabled
                        && (GPU.lcdcOamInterruptEnabled
                            || (GPU.lcdcLycLyCoincidenceInterruptEnabled && GPU.lyCompare == y)))
                    {
                        Z80.lcdcInterruptRequested = true;
                    }
                    ExecuteProcessor(800);
                    GPU.lcdcMode = LcdcModeType.TransferingData;
                    ExecuteProcessor(1720);
                    GPU.lcdcMode = LcdcModeType.HBlank;
                    if (Z80.lcdcInterruptEnabled && GPU.lcdcHBlankInterruptEnabled)
                    {
                        Z80.lcdcInterruptRequested = true;
                    }
                    ExecuteProcessor(2040);
                    AddTicksPerScanLine();
                }
            }
            GPU.lcdcMode = LcdcModeType.VBlank;
            if (Z80.vBlankInterruptEnabled)
            {
                Z80.vBlankInterruptRequested = true;
            }
            if (Z80.lcdcInterruptEnabled && GPU.lcdcVBlankInterruptEnabled)
            {
                Z80.lcdcInterruptRequested = true;
            }
            for (int y = 144; y <= 153; y++)
            {
                GPU.ly = y;
                if (Z80.lcdcInterruptEnabled && GPU.lcdcLycLyCoincidenceInterruptEnabled
                    && GPU.lyCompare == y)
                {
                    Z80.lcdcInterruptRequested = true;
                }
                ExecuteProcessor(4560);
                AddTicksPerScanLine();
            }
        }

        private void AddTicksPerScanLine()
        {
            switch (Z80.timerFrequency)
            {
                case TimerFrequencyType.hz4096:
                    scanLineTicks += 0.44329004329004329004329004329004;
                    break;
                case TimerFrequencyType.hz16384:
                    scanLineTicks += 1.7731601731601731601731601731602;
                    break;
                case TimerFrequencyType.hz65536:
                    scanLineTicks += 7.0926406926406926406926406926407;
                    break;
                case TimerFrequencyType.hz262144:
                    scanLineTicks += 28.370562770562770562770562770563;
                    break;
            }
            while (scanLineTicks >= 1.0)
            {
                scanLineTicks -= 1.0;
                if (Z80.timerCounter == 0xFF)
                {
                    Z80.timerCounter = Z80.timerModulo;
                    if (Z80.lcdcInterruptEnabled && Z80.timerOverflowInterruptEnabled)
                    {
                        Z80.timerOverflowInterruptRequested = true;
                    }
                }
                else
                {
                    Z80.timerCounter++;
                }
            }
        }

        //wykonywanie instrukcji procesora
        private void ExecuteProcessor(int maxTicks)
        {
            do
            {
                z80.Step();
                if (z80.halted)
                {
                    Z80.ticks = ((maxTicks - Z80.ticks) & 0x03);
                    return;
                }
            } while (Z80.ticks < maxTicks);
            Z80.ticks -= maxTicks;
        }

        //renderowanie obrazu
        private void RenderFrame()
        {
            g.DrawImage(bitmap, 0, menuStrip1.Height, width, height);
        }

        private void InitFrame()
        {
            rectangle = new Rectangle(0, 0, 160, 144);
            StartPosition = FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }

        //ustawianie wielkości obrazu
        private void SetImageSize(int scale)
        {
            width = scale * 160;
            height = scale * 144;
            ClientSize = new Size(width, height + menuStrip1.Height);
            InitGraphics();
            Invalidate();
        }

        //ustawienie grafiki
        private void InitGraphics()
        {
            if (g != null)
            {
                g.Dispose();
            }
            g = CreateGraphics();
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        }

        //ustawienie wyrenderowanego obrazu
        private void InitImage()
        {
            InitGraphics();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = 0xFF000000;
            }
            GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            IntPtr pointer = Marshal.UnsafeAddrOfPinnedArrayElement(pixels, 0);
            bitmap = new Bitmap(160, 144, 160 * 4, PixelFormat.Format32bppPArgb, pointer);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitImage();
            SetImageSize(2);
            stopWatch.Start();
            nextFrameStart = stopWatch.ElapsedTicks;
            Application.Idle += OnApplicationIdle;
        }

        //sprawdzenie czy naciskamy przycisk czy go puszczamy
        private void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            memory.KeyChanged(e.KeyCode, true);
        }

        private void GameForm_KeyUp(object sender, KeyEventArgs e)
        {
            memory.KeyChanged(e.KeyCode, false);
        }

        //malowanie obrazu na ekranie
        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(bitmap, 0, menuStrip1.Height, width, height);
        }


        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            
        }

        //ładowanie romu do pamięci
        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                string fileName = openFileDialog1.FileName;
                ROMLoader romLoader = new ROMLoader();
                game = romLoader.Load(fileName);
                memory = new Memory();
                gpu = new GPU(memory);
                z80 = new Z80(memory);
                memory.cartridge = game.cartridge;
                z80.Reset();
            }
        }
    }
}