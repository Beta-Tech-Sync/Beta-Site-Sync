"""Capture the built MAUI app and its inventory page on the running emulator."""

import re
import subprocess
import time
import xml.etree.ElementTree as ET

DUMP_PATH = "/sdcard/stock-ui.xml"


def adb(*args):
    return subprocess.check_output(["adb", *args], text=True, timeout=15)


def screenshot(path):
    with open(path, "wb") as output:
        subprocess.run(["adb", "exec-out", "screencap", "-p"], stdout=output, check=True)


def wait_for_ui(find_match, failure_message):
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        try:
            adb("shell", "rm", "-f", DUMP_PATH)
            adb("shell", "uiautomator", "dump", DUMP_PATH)
            screen = ET.fromstring(adb("shell", "cat", DUMP_PATH))
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired, ET.ParseError) as error:
            print(f"Retrying Android UI snapshot: {error}", flush=True)
        else:
            match = find_match(screen)
            if match is not None:
                return match
        time.sleep(2)
    raise RuntimeError(failure_message)


def inventory_tab(screen):
    for node in screen.iter("node"):
        if "Inventory Tab." in node.get("content-desc", "") or node.get("text") == "Inventory":
            bounds = list(map(int, re.findall(r"\d+", node.get("bounds", ""))))
            if len(bounds) == 4:
                left, top, right, bottom = bounds
                return (left + right) // 2, (top + bottom) // 2
    return None


def inventory_ready(screen):
    for node in screen.iter("node"):
        for attribute in ("text", "content-desc"):
            if "Ready. Saved changes stay in the workspace shown above." in node.get(attribute, ""):
                return node
    return None


target = wait_for_ui(inventory_tab, "Inventory navigation was not found in the built Android app.")
screenshot("android-dashboard.png")
adb("shell", "input", "tap", str(target[0]), str(target[1]))
wait_for_ui(inventory_ready, "Android stock workspace did not appear.")
screenshot("android-stock.png")
print("Android stock workspace rendered; preview captured.")
