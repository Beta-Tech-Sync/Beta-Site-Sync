"""Capture the built MAUI app and its inventory page on the running emulator."""
import re, subprocess, time, xml.etree.ElementTree as ET

def adb(*args):
    return subprocess.check_output(["adb", *args], text=True)
subprocess.run(["adb","exec-out","screencap","-p"],stdout=open("android-dashboard.png","wb"),check=True)
adb("shell","uiautomator","dump","/sdcard/stock-ui.xml")
tree=ET.fromstring(adb("shell","cat","/sdcard/stock-ui.xml"))
target=None
for node in tree.iter("node"):
    name=node.get("text","")+" "+node.get("content-desc","")
    if "Inventory Tab." in name or node.get("text","") == "Inventory":
        values=list(map(int,re.findall(r"\d+",node.get("bounds",""))))
        if len(values)==4:target=((values[0]+values[2])//2,(values[1]+values[3])//2);break
if target is None:raise RuntimeError("Inventory navigation was not found in the built Android app.")
adb("shell","input","tap",str(target[0]),str(target[1]))
for _ in range(20):
    time.sleep(2)
    adb("shell","uiautomator","dump","/sdcard/stock-ui.xml")
    screen=adb("shell","cat","/sdcard/stock-ui.xml")
    if "Ready. Saved changes stay in the workspace shown above." in screen:
        subprocess.run(["adb","exec-out","screencap","-p"],stdout=open("android-stock.png","wb"),check=True)
        print("Android stock workspace rendered; preview captured.")
        break
else:raise RuntimeError("Android stock workspace did not appear.")
