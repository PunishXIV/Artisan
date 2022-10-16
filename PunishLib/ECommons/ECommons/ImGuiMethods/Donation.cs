using Dalamud.Interface.Colors;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    public static class Donation
    {
        public static void PrintDonationInfo()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            ImGuiEx.ButtonCopy("Bitcoin (BTC): $COPY (preferred)", "bc1qwzh7mc3glcdemyg9xpvr7cfuc2nxl8u87x73e4");
            ImGuiEx.ButtonCopy("USDT (TRC20): $COPY (preferred)", "TBNN99wdCzPX4HavCjiooq3NjvujLgqfoK");
            ImGuiEx.ButtonCopy("USDC (TRC20): $COPY (preferred)", "TBNN99wdCzPX4HavCjiooq3NjvujLgqfoK");
            ImGui.PopStyleColor();
            if (ImGui.CollapsingHeader("Other wallets:"))
            {
                ImGuiEx.ButtonCopy("USDT (ERC20): $COPY", "0xA46D5cD23C7586b0817413682cdeCC8E3CdB590F");
                ImGuiEx.ButtonCopy("USDC (ERC20): $COPY", "0xA46D5cD23C7586b0817413682cdeCC8E3CdB590F");
                ImGuiEx.ButtonCopy("USDC (SPL): $COPY", "GZtgrwgMM1MAgCBnDa5JJoFsuCLN9iRhGYFDzd8u7d3j");
                ImGuiEx.ButtonCopy("Litecoin (LTC): $COPY", "ltc1qrgc802qzdez2q2v6ds293qrglfzj2kvwm5dl4f");
                ImGuiEx.ButtonCopy("Ethereum (ETH): $COPY", "0xA46D5cD23C7586b0817413682cdeCC8E3CdB590F");
                ImGuiEx.ButtonCopy("BUSD (BEP20): $COPY", "0xA46D5cD23C7586b0817413682cdeCC8E3CdB590F");
            }
            ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, "Attention! Malware programs may replace crypto wallet address inside your clipboard. ALWAYS double-check destination address before sending any funds.");

        }

        public static void DonationTabDraw()
        {
            ImGuiEx.TextWrapped("If you have found this plugin useful and wish to thank me for making it, you may send any amount of any of the following cryptocurrencies to any of the following wallets:");
            PrintDonationInfo();
            ImGuiEx.TextWrapped("Regardless of donations, plugin will continue to be supported and updated.");
        }
    }
}
