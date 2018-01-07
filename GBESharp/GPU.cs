using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBESharp
{
    public enum LcdcModeType
    {
        HBlank = 0,
        VBlank = 1,
        SearchingOamRam = 2,
        TransferingData = 3
    }

    class GPU
    {
        //domyślna pleta kolorów wykorzystywana w emulatorach
        public const uint white = 0xFFFFFFFF;
        public const uint lightGray = 0xFFAAAAAA;
        public const uint darkGray = 0xFF555555;
        public const uint black = 0xFF000000;
        public static uint[] backgroundPalette = { white, lightGray, darkGray, black };
        public static uint[] objectPallete0 = { white, lightGray, darkGray, black };
        public static uint[] objectPallete1 = { white, lightGray, darkGray, black };
        public static bool backgroundAndWindowTileDataSelect;
        public static bool backgroundTileMapDisplaySelect;
        public uint[,] backgroundBuffer = new uint[256, 256]; //buffor tła
        public static bool[,] backgroundTileInvalidated = new bool[32, 32]; // tablica dla tła
        public static bool invalidateAllBackgroundTilesRequest;
        public uint[,,,] spriteTile = new uint[256, 8, 8, 2]; //tablica dla tilsetów
        public static bool[] spriteTileInvalidated = new bool[256];
        public static bool invalidateAllSpriteTilesRequest;
        public uint[,] windowBuffer = new uint[144, 168]; //buffor ekranu
        public static bool lcdcLycLyCoincidenceInterruptEnabled;
        public static bool lcdcOamInterruptEnabled;
        public static bool lcdcVBlankInterruptEnabled;
        public static bool lcdcHBlankInterruptEnabled;
        public static bool windowTileMapDisplaySelect;
        public static bool windowDisplayed;
        public static bool largeSprites;
        public static bool spritesDisplayed;
        public static bool backgroundDisplayed;
        public static int scrollX, scrollY; //rejestry przesuwanie tła
        public static int windowX, windowY;
        public static int lyCompare, ly;
        public static LcdcModeType lcdcMode; //tryb wyświetlania obrazu

        Memory memory;

        public GPU(Memory memory)
        {
            this.memory = memory;
        }

        //aktualizacja tilestów
        public void UpdateSpriteTiles()
        {
            for (int i = 0; i < 256; i++) //może być maksymalnie 256 tilesetów ustawionych na mapie
            {
                if (spriteTileInvalidated[i] || invalidateAllSpriteTilesRequest)
                {
                    spriteTileInvalidated[i] = false;
                    int address = i << 4;
                    for (int y = 0; y < 8; y++) //maksymalny rozmiar tilesetu 8x8
                    {
                        int lowByte = memory.vRam[address++];
                        int highByte = memory.vRam[address ++] << 1;
                        for (int x = 7; x >=0; x--)
                        {
                            int palletteIndex = (0x02 & highByte) | (0x01 & lowByte); //sprawdzenie z pamięci jaki kolor powinien mieć sprite
                            lowByte >>= 1;
                            highByte >>= 1;
                            if (palletteIndex > 0)
                            {
                                //nadawanie spraitom danego koloru
                                spriteTile[i, y, x, 0] = objectPallete0[palletteIndex];
                                spriteTile[i, y, x, 1] = objectPallete1[palletteIndex];
                            }
                            else
                            {
                                //nadawanie spraitom przezroczystości
                                spriteTile[i, y, x, 0] = 0;
                                spriteTile[i, y, x, 1] = 1;
                            }
                        }
                    }
                }
            }
            invalidateAllSpriteTilesRequest = false;
        }

        //aktualizacja tła
        public void UpdateBackground()
        {
            int tileMapAddress = backgroundTileMapDisplaySelect ? 0x1C00 : 0x1800;

            if(backgroundTileMapDisplaySelect)
            {
                for(int i=0;i<32;i++) //tilsety tła wynosza 32x32
                {
                    for(int j=0;j<32;j++,tileMapAddress++)
                    {
                        if(backgroundTileInvalidated[i,j] || invalidateAllBackgroundTilesRequest)
                        {
                            backgroundTileInvalidated[i,j] = false;
                            int tileDataAddress = memory.vRam[tileMapAddress]<<4; //pobieranie tilsetów z pamięci 
                            int y = i << 3;
                            int x = j << 3;
                            for(int k=0;k<8;k++)
                            {
                                int lowByte = memory.vRam[tileDataAddress++];
                                int highByte = memory.vRam[tileDataAddress++] << 1;
                                for(int b=7;b>=0;b--)
                                {
                                    backgroundBuffer[y + k, x + b] = backgroundPalette[(0x02 & highByte) | (0x01 & lowByte)];
                                    lowByte >>= 1;
                                    highByte >>= 1;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                for(int i=0;i<32;i++)
                {
                    for(int j=0;j<32;j++,tileMapAddress++)
                    {
                        if(backgroundTileInvalidated[i,j] || invalidateAllBackgroundTilesRequest)
                        {
                            backgroundTileInvalidated[i, j] = false;
                            int tileDataAddress = memory.vRam[tileMapAddress];
                            if(tileDataAddress > 127)
                            {
                                tileDataAddress -= 256;
                            }
                            tileDataAddress = 0x1000 + (tileDataAddress << 4);
                            int y = i << 3;
                            int x = j << 3;
                            for(int k=0;k<8;k++)
                            {
                                int lowByte = memory.vRam[tileDataAddress++];
                                int highByte = memory.vRam[tileDataAddress++] << 1;
                                for(int b=7;b>=0;b--)
                                {
                                    backgroundBuffer[y + k, x + b] = backgroundPalette[(0x02 & highByte) | (0x01 & lowByte)];
                                    lowByte >>= 1;
                                    highByte >>= 1;
                                }
                            }
                        }
                    }
                }
            }
            invalidateAllBackgroundTilesRequest = false;
        }

        //aktualizacja HUD
        public void UpdateWindow()
        {
            int tileMapAddress = windowTileMapDisplaySelect ? 0x1C00 : 0x1800;

            if (backgroundAndWindowTileDataSelect)
            {
                for (int i = 0; i < 18; i++)
                {
                    for (int j = 0; j < 21; j++)
                    {
                        if (backgroundTileInvalidated[i, j] || invalidateAllBackgroundTilesRequest)
                        {
                            int tileDataAddress = memory.vRam[tileMapAddress + ((i << 5) | j)] << 4;
                            int y = i << 3;
                            int x = j << 3;
                            for (int k = 0; k < 8; k++)
                            {
                                int lowByte = memory.vRam[tileDataAddress++];
                                int highByte = memory.vRam[tileDataAddress++] << 1;
                                for (int b = 7; b >= 0; b--)
                                {
                                    windowBuffer[y + k, x + b] = backgroundPalette[(0x02 & highByte) | (0x01 & lowByte)];
                                    lowByte >>= 1;
                                    highByte >>= 1;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 18; i++)
                {
                    for (int j = 0; j < 21; j++)
                    {
                        if (backgroundTileInvalidated[i, j] || invalidateAllBackgroundTilesRequest)
                        {
                            int tileDataAddress = memory.vRam[tileMapAddress + ((i << 5) | j)];
                            if (tileDataAddress > 127)
                            {
                                tileDataAddress -= 256;
                            }
                            tileDataAddress = 0x1000 + (tileDataAddress << 4);
                            int y = i << 3;
                            int x = j << 3;
                            for (int k = 0; k < 8; k++)
                            {
                                int lowByte = memory.vRam[tileDataAddress++];
                                int highByte = memory.vRam[tileDataAddress++] << 1;
                                for (int b = 7; b >= 0; b--)
                                {
                                    windowBuffer[y + k, x + b] = backgroundPalette[(0x02 & highByte) | (0x01 & lowByte)];
                                    lowByte >>= 1;
                                    highByte >>= 1;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
