using System;
using System.Windows.Forms;

namespace GBESharp
{
    interface ICartridge
    {
        int ReadByte(int address);
        void WriteByte(int address, int valume);
    }

    class Memory
    {
        //Input
        public bool leftKeyPressed;
        public bool rightKeyPressed;
        public bool upKeyPressed;
        public bool downKeyPressed;
        public bool aButtonPressed;
        public bool bButtonPressed;
        public bool startButtonPressed;
        public bool selectButtonPressed;
        public bool keyP14, keyP15;

        public ICartridge cartridge;
        public byte[] highRam;
        public byte[] vRam; //video Ram
        public byte[] wRam; //work Ram
        public byte[] oam;

        public Memory()
        {
            highRam = new byte[256];
            vRam = new byte[8 * 1024];
            wRam = new byte[8 * 1024];
            oam = new byte[256];
        }

        public void WriteWord(int address, int valume)
        {
            WriteByte(address, valume & 0xFF);
            WriteByte(address + 1, valume >> 8);
        }

        public void WriteByte(int address, int value)
        {
            if (address >= 0xC000 && address <= 0xDFFF)
            {
                wRam[address - 0xC000] = (byte)value;
            }
            else if (address >= 0xFE00 && address <= 0xFEFF)
            {
                oam[address - 0xFE00] = (byte)value;
            }
            else if (address >= 0xFF80 && address <= 0xFFFE)
            {
                highRam[0xFF & address] = (byte)value;
            }
            else if (address >= 0x8000 && address <= 0x9FFF)
            {
                int videoRamIndex = address - 0x8000;
                vRam[videoRamIndex] = (byte)value;
                if (address < 0x9000)
                {
                    GPU.spriteTileInvalidated[videoRamIndex >> 4] = true;
                }
                if (address < 0x9800)
                {
                    GPU.invalidateAllBackgroundTilesRequest = true;
                }
                else if (address >= 0x9C00)
                {
                    int tileIndex = address - 0x9C00;
                    GPU.backgroundTileInvalidated[tileIndex >> 5, tileIndex & 0x1F] = true;
                }
                else
                {
                    int tileIndex = address - 0x9800;
                    GPU.backgroundTileInvalidated[tileIndex >> 5, tileIndex & 0x1F] = true;
                }
            }
            else if (address <= 0x7FFF || (address >= 0xA000 && address <= 0xBFFF))
            {
                cartridge.WriteByte(address, value);
            }
            else if (address >= 0xE000 && address <= 0xFDFF)
            {
                wRam[address - 0xE000] = (byte)value;
            }
            else
            {
                switch (address)
                {
                    case 0xFF00: // key pad
                        keyP14 = (value & 0x10) != 0x10;
                        keyP15 = (value & 0x20) != 0x20;
                        break;
                    case 0xFF04: // Timer divider            
                        break;
                    case 0xFF05: // Timer counter
                        Z80.timerCounter = value;
                        break;
                    case 0xFF06: // Timer modulo
                        Z80.timerModulo = value;
                        break;
                    case 0xFF07:  // Time Control
                        Z80.timerRunning = (value & 0x04) == 0x04;
                        Z80.timerFrequency = (TimerFrequencyType)(0x03 & value);
                        break;
                    case 0xFF0F: // Interrupt Flag (an interrupt request)
                        Z80.keyPressedInterruptRequested = (value & 0x10) == 0x10;
                        Z80.serialIOTransferCompleteInterruptRequested = (value & 0x08) == 0x08;
                        Z80.timerOverflowInterruptRequested = (value & 0x04) == 0x04;
                        Z80.lcdcInterruptRequested = (value & 0x02) == 0x02;
                        Z80.vBlankInterruptRequested = (value & 0x01) == 0x01;
                        break;
                    case 0xFF40:
                        { // LCDC control
                            bool _backgroundAndWindowTileDataSelect = GPU.backgroundAndWindowTileDataSelect;
                            bool _backgroundTileMapDisplaySelect = GPU.backgroundTileMapDisplaySelect;
                            bool _windowTileMapDisplaySelect = GPU.windowTileMapDisplaySelect;

                            Z80.lcdControlOperationEnabled = (value & 0x80) == 0x80;
                            GPU.windowTileMapDisplaySelect = (value & 0x40) == 0x40;
                            GPU.windowDisplayed = (value & 0x20) == 0x20;
                            GPU.backgroundAndWindowTileDataSelect = (value & 0x10) == 0x10;
                            GPU.backgroundTileMapDisplaySelect = (value & 0x08) == 0x08;
                            GPU.largeSprites = (value & 0x04) == 0x04;
                            GPU.spritesDisplayed = (value & 0x02) == 0x02;
                            GPU.backgroundDisplayed = (value & 0x01) == 0x01;

                            if (_backgroundAndWindowTileDataSelect != GPU.backgroundAndWindowTileDataSelect
                                || _backgroundTileMapDisplaySelect != GPU.backgroundTileMapDisplaySelect
                                || _windowTileMapDisplaySelect != GPU.windowTileMapDisplaySelect)
                            {
                                GPU.invalidateAllBackgroundTilesRequest = true;
                            }

                            break;
                        }
                    case 0xFF41: // LCDC Status
                        GPU.lcdcLycLyCoincidenceInterruptEnabled = (value & 0x40) == 0x40;
                        GPU.lcdcOamInterruptEnabled = (value & 0x20) == 0x20;
                        GPU.lcdcVBlankInterruptEnabled = (value & 0x10) == 0x10;
                        GPU.lcdcHBlankInterruptEnabled = (value & 0x08) == 0x08;
                        GPU.lcdcMode = (LcdcModeType)(value & 0x03);
                        break;
                    case 0xFF42: // Scroll Y;
                        GPU.scrollY = value;
                        break;
                    case 0xFF43: // Scroll X;
                        GPU.scrollX = value;
                        break;
                    case 0xFF44: // LY
                        GPU.ly = value;
                        break;
                    case 0xFF45: // LY Compare
                        GPU.lyCompare = value;
                        break;
                    case 0xFF46: // Memory Transfer
                        value <<= 8;
                        for (int i = 0; i < 0x8C; i++)
                        {
                            WriteByte(0xFE00 | i, ReadByte(value | i));
                        }
                        break;
                    case 0xFF47: // Background palette
                        Console.WriteLine("[0xFF47] = {0:X}", value);
                        for (int i = 0; i < 4; i++)
                        {
                            switch (value & 0x03)
                            {
                                case 0:
                                    GPU.backgroundPalette[i] = 0xFFFFFFFF;
                                    break;
                                case 1:
                                    GPU.backgroundPalette[i] = 0xFFAAAAAA;
                                    break;
                                case 2:
                                    GPU.backgroundPalette[i] = 0xFF555555;
                                    break;
                                case 3:
                                    GPU.backgroundPalette[i] = 0xFF000000;
                                    break;
                            }
                            value >>= 2;
                        }
                        GPU.invalidateAllBackgroundTilesRequest = true;
                        break;
                    case 0xFF48: // Object palette 0
                        for (int i = 0; i < 4; i++)
                        {
                            switch (value & 0x03)
                            {
                                case 0:
                                    GPU.objectPallete0[i] = 0xFFFFFFFF;
                                    break;
                                case 1:
                                    GPU.objectPallete0[i] = 0xFFAAAAAA;
                                    break;
                                case 2:
                                    GPU.objectPallete0[i] = 0xFF555555;
                                    break;
                                case 3:
                                    GPU.objectPallete0[i] = 0xFF000000;
                                    break;
                            }
                            value >>= 2;
                        }
                        GPU.invalidateAllSpriteTilesRequest = true;
                        break;
                    case 0xFF49: // Object palette 1
                        for (int i = 0; i < 4; i++)
                        {
                            switch (value & 0x03)
                            {
                                case 0:
                                    GPU.objectPallete1[i] = 0xFFFFFFFF;
                                    break;
                                case 1:
                                    GPU.objectPallete1[i] = 0xFFAAAAAA;
                                    break;
                                case 2:
                                    GPU.objectPallete1[i] = 0xFF555555;
                                    break;
                                case 3:
                                    GPU.objectPallete1[i] = 0xFF000000;
                                    break;
                            }
                            value >>= 2;
                        }
                        GPU.invalidateAllSpriteTilesRequest = true;
                        break;
                    case 0xFF4A: // Window Y
                        GPU.windowY = value;
                        break;
                    case 0xFF4B: // Window X
                        GPU.windowX = value;
                        break;
                    case 0xFFFF: // Interrupt Enable
                        Z80.keyPressedInterruptEnabled = (value & 0x10) == 0x10;
                        Z80.serialIOTransferCompleteInterruptEnabled = (value & 0x08) == 0x08;
                        Z80.timerOverflowInterruptEnabled = (value & 0x04) == 0x04;
                        Z80.lcdcInterruptEnabled = (value & 0x02) == 0x02;
                        Z80.vBlankInterruptEnabled = (value & 0x01) == 0x01;
                        break;
                }
            }
        }

        public int ReadWord(int address)
        {
            int low = ReadByte(address);
            int high = ReadByte(address + 1);
            return (high << 8) | low;
        }

        public int ReadByte(int address)
        {
            if (address <= 0x7FFF || (address >= 0xA000 && address <= 0xBFFF))
            {
                return cartridge.ReadByte(address);
            }
            else if (address >= 0x8000 && address <= 0x9FFF)
            {
                return vRam[address - 0x8000];
            }
            else if (address >= 0xC000 && address <= 0xDFFF)
            {
                return wRam[address - 0xC000];
            }
            else if (address >= 0xE000 && address <= 0xFDFF)
            {
                return wRam[address - 0xE000];
            }
            else if (address >= 0xFE00 && address <= 0xFEFF)
            {
                return oam[address - 0xFE00];
            }
            else if (address >= 0xFF80 && address <= 0xFFFE)
            {
                return highRam[0xFF & address];
            }
            else
            {
                switch (address)
                {
                    case 0xFF00: // key pad
                        if (keyP14)
                        {
                            int value = 0;
                            if (!downKeyPressed)
                            {
                                value |= 0x08;
                            }
                            if (!upKeyPressed)
                            {
                                value |= 0x04;
                            }
                            if (!leftKeyPressed)
                            {
                                value |= 0x02;
                            }
                            if (!rightKeyPressed)
                            {
                                value |= 0x01;
                            }
                            return value;
                        }
                        else if (keyP15)
                        {
                            int value = 0;
                            if (!startButtonPressed)
                            {
                                value |= 0x08;
                            }
                            if (!selectButtonPressed)
                            {
                                value |= 0x04;
                            }
                            if (!bButtonPressed)
                            {
                                value |= 0x02;
                            }
                            if (!aButtonPressed)
                            {
                                value |= 0x01;
                            }
                            return value;
                        }
                        break;
                    case 0xFF04: // Timer divider
                        return Z80.ticks & 0xFF;
                    case 0xFF05: // Timer counter
                        return Z80.timerCounter & 0xFF;
                    case 0xFF06: // Timer modulo
                        return Z80.timerModulo & 0xFF;
                    case 0xFF07:
                        { // Time Control
                            int value = 0;
                            if (Z80.timerRunning)
                            {
                                value |= 0x04;
                            }
                            value |= (int)Z80.timerFrequency;
                            return value;
                        }
                    case 0xFF0F:
                        { // Interrupt Flag (an interrupt request)
                            int value = 0;
                            if (Z80.keyPressedInterruptRequested)
                            {
                                value |= 0x10;
                            }
                            if (Z80.serialIOTransferCompleteInterruptRequested)
                            {
                                value |= 0x08;
                            }
                            if (Z80.timerOverflowInterruptRequested)
                            {
                                value |= 0x04;
                            }
                            if (Z80.lcdcInterruptRequested)
                            {
                                value |= 0x02;
                            }
                            if (Z80.vBlankInterruptRequested)
                            {
                                value |= 0x01;
                            }
                            return value;
                        }
                    case 0xFF40:
                        { // LCDC control
                            int value = 0;
                            if (Z80.lcdControlOperationEnabled)
                            {
                                value |= 0x80;
                            }
                            if (GPU.windowTileMapDisplaySelect)
                            {
                                value |= 0x40;
                            }
                            if (GPU.windowDisplayed)
                            {
                                value |= 0x20;
                            }
                            if (GPU.backgroundAndWindowTileDataSelect)
                            {
                                value |= 0x10;
                            }
                            if (GPU.backgroundTileMapDisplaySelect)
                            {
                                value |= 0x08;
                            }
                            if (GPU.largeSprites)
                            {
                                value |= 0x04;
                            }
                            if (GPU.spritesDisplayed)
                            {
                                value |= 0x02;
                            }
                            if (GPU.backgroundDisplayed)
                            {
                                value |= 0x01;
                            }
                            return value;
                        }
                    case 0xFF41:
                        {// LCDC Status
                            int value = 0;
                            if (GPU.lcdcLycLyCoincidenceInterruptEnabled)
                            {
                                value |= 0x40;
                            }
                            if (GPU.lcdcOamInterruptEnabled)
                            {
                                value |= 0x20;
                            }
                            if (GPU.lcdcVBlankInterruptEnabled)
                            {
                                value |= 0x10;
                            }
                            if (GPU.lcdcHBlankInterruptEnabled)
                            {
                                value |= 0x08;
                            }
                            if (GPU.ly == GPU.lyCompare)
                            {
                                value |= 0x04;
                            }
                            value |= (int)GPU.lcdcMode;
                            return value;
                        }
                    case 0xFF42: // Scroll Y
                        return GPU.scrollY;
                    case 0xFF43: // Scroll X
                        return GPU.scrollX;
                    case 0xFF44: // LY
                        return GPU.ly;
                    case 0xFF45: // LY Compare
                        return GPU.lyCompare;
                    case 0xFF47:
                        { // Background palette
                            GPU.invalidateAllBackgroundTilesRequest = true;
                            int value = 0;
                            for (int i = 3; i >= 0; i--)
                            {
                                value <<= 2;
                                switch (GPU.backgroundPalette[i])
                                {
                                    case 0xFF000000:
                                        value |= 3;
                                        break;
                                    case 0xFF555555:
                                        value |= 2;
                                        break;
                                    case 0xFFAAAAAA:
                                        value |= 1;
                                        break;
                                    case 0xFFFFFFFF:
                                        break;
                                }
                            }
                            return value;
                        }
                    case 0xFF48:
                        { // Object palette 0
                            GPU.invalidateAllSpriteTilesRequest = true;
                            int value = 0;
                            for (int i = 3; i >= 0; i--)
                            {
                                value <<= 2;
                                switch (GPU.objectPallete0[i])
                                {
                                    case 0xFF000000:
                                        value |= 3;
                                        break;
                                    case 0xFF555555:
                                        value |= 2;
                                        break;
                                    case 0xFFAAAAAA:
                                        value |= 1;
                                        break;
                                    case 0xFFFFFFFF:
                                        break;
                                }
                            }
                            return value;
                        }
                    case 0xFF49:
                        { // Object palette 1
                            GPU.invalidateAllSpriteTilesRequest = true;
                            int value = 0;
                            for (int i = 3; i >= 0; i--)
                            {
                                value <<= 2;
                                switch (GPU.objectPallete1[i])
                                {
                                    case 0xFF000000:
                                        value |= 3;
                                        break;
                                    case 0xFF555555:
                                        value |= 2;
                                        break;
                                    case 0xFFAAAAAA:
                                        value |= 1;
                                        break;
                                    case 0xFFFFFFFF:
                                        break;
                                }
                            }
                            return value;
                        }
                    case 0xFF4A: // Window Y
                        return GPU.windowY;
                    case 0xFF4B: // Window X
                        return GPU.windowX;
                    case 0xFFFF:
                        { // Interrupt Enable
                            int value = 0;
                            if (Z80.keyPressedInterruptEnabled)
                            {
                                value |= 0x10;
                            }
                            if (Z80.serialIOTransferCompleteInterruptEnabled)
                            {
                                value |= 0x08;
                            }
                            if (Z80.timerOverflowInterruptEnabled)
                            {
                                value |= 0x04;
                            }
                            if (Z80.lcdcInterruptEnabled)
                            {
                                value |= 0x02;
                            }
                            if (Z80.vBlankInterruptEnabled)
                            {
                                value |= 0x01;
                            }
                            return value;
                        }
                }
            }
            return 0;
        }

        public void KeyChanged(Keys keyCode, bool pressed)
        {
            switch (keyCode)
            {
                case Keys.Z:
                    bButtonPressed = pressed;
                    break;
                case Keys.X:
                    aButtonPressed = pressed;
                    break;
                case Keys.Enter:
                    startButtonPressed = pressed;
                    break;
                case Keys.Space:
                    selectButtonPressed = pressed;
                    break;
                case Keys.Up:
                    upKeyPressed = pressed;
                    break;
                case Keys.Down:
                    downKeyPressed = pressed;
                    break;
                case Keys.Left:
                    leftKeyPressed = pressed;
                    break;
                case Keys.Right:
                    rightKeyPressed = pressed;
                    break;
            }
        }
    }
}
