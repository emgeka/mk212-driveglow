export function Name() { return "OMOTON MK212"; }
export function Publisher() { return "emgeka"; }
export function VendorId() { return 0x36B0; }
export function ProductId() { return 0x3142; }
export function Type() { return "hid"; }
export function DeviceType() { return "keyboard"; }
export function Size() { return [19, 6]; }
export function DefaultPosition() { return [0, 0]; }
export function DefaultScale() { return 1.0; }
export function ImageUrl() { return "https://assets.signalrgb.com/devices/default/keyboards/full-size-keyboard-render.png"; }

/* global
AccentBarBrightness:readonly
KeyboardBrightness:readonly
*/

export function ControllableParameters() {
	return [
		{
			property: "KeyboardBrightness", group: "lighting", label: "Keyboard Brightness",
			description: "Controls the brightness of the single-color keyboard zone.",
			type: "number", min: "0", max: "160", default: "160"
		},
		{
			property: "AccentBarBrightness", group: "lighting", label: "Accent Bar Brightness",
			description: "Set to zero to release the accent bar for control by another application.",
			type: "number", min: "0", max: "160", default: "160"
		}
	];
}

export function DeviceMessages() {
	return [
		{
			property: "Limited Functionality",
			message: "Limited Functionality",
			tooltip: "The stock firmware exposes one global keyboard color and one accent-bar color, rather than per-key RGB streaming."
		}
	];
}

const ledNames = ["Keyboard Zone", "Accent Bar"];
const ledPositions = [[9, 3], [2, 0]];

const viaSetCustomValue = 0x07;
const keyboardChannel = 0x03;
const accentBarChannel = 0x04;
const valueBrightness = 0x01;
const valueEffect = 0x02;
const valueColor = 0x04;
const solidColorEffect = 0x01;
const accentBarSolidEffect = 0x05;

let lastRed = -1;
let lastGreen = -1;
let lastBlue = -1;
let lastBrightness = -1;
let lastAccentRed = -1;
let lastAccentGreen = -1;
let lastAccentBlue = -1;
let lastAccentBrightness = -1;
let renderDivider = 0;

export function LedNames() {
	return ledNames;
}

export function LedPositions() {
	return ledPositions;
}

export function Validate(endpoint) {
	return endpoint.interface === 1 && endpoint.usage === 0x0061 && endpoint.usage_page === 0xFF60;
}

export function Initialize() {
	lastRed = -1;
	lastGreen = -1;
	lastBlue = -1;
	lastBrightness = -1;
	lastAccentRed = -1;
	lastAccentGreen = -1;
	lastAccentBlue = -1;
	lastAccentBrightness = -1;
	renderDivider = 0;

	setKeyboardEffect(solidColorEffect);
	setKeyboardBrightness(clampByte(Number(KeyboardBrightness), 160));

	const accentBrightness = clampByte(Number(AccentBarBrightness), 160);

	if(accentBrightness > 0) {
		setAccentBarEffect(accentBarSolidEffect);
	}

	setAccentBarBrightness(accentBrightness);
}

export function Render() {
	// VIA custom values are configuration-oriented, so updates are limited to about 30 fps.
	renderDivider = (renderDivider + 1) % 2;

	if(renderDivider !== 0) {
		return;
	}

	updateKeyboard();
	updateAccentBar();
}

function updateKeyboard() {

	const brightness = clampByte(Number(KeyboardBrightness), 160);

	if(brightness !== lastBrightness) {
		setKeyboardBrightness(brightness);
		lastBrightness = brightness;
	}

	const color = device.color(ledPositions[0][0], ledPositions[0][1]);
	const red = clampByte(color[0], 255);
	const green = clampByte(color[1], 255);
	const blue = clampByte(color[2], 255);

	if(red !== lastRed || green !== lastGreen || blue !== lastBlue) {
		const hsv = rgbToQmkHsv(red, green, blue);
		setKeyboardColor(hsv[0], hsv[1]);
		lastRed = red;
		lastGreen = green;
		lastBlue = blue;
	}
}

function updateAccentBar() {

	const accentBrightness = clampByte(Number(AccentBarBrightness), 160);

	if(accentBrightness !== lastAccentBrightness) {
		if(lastAccentBrightness === 0 && accentBrightness > 0) {
			setAccentBarEffect(accentBarSolidEffect);
		}

		setAccentBarBrightness(accentBrightness);
		lastAccentBrightness = accentBrightness;
	}

	// A value of zero intentionally releases the accent bar. This lets another
	// application control it without SignalRGB continuously overwriting its color.
	if(accentBrightness === 0) {
		lastAccentRed = -1;
		lastAccentGreen = -1;
		lastAccentBlue = -1;

		return;
	}

	const accentColor = device.color(ledPositions[1][0], ledPositions[1][1]);
	const accentRed = clampByte(accentColor[0], 255);
	const accentGreen = clampByte(accentColor[1], 255);
	const accentBlue = clampByte(accentColor[2], 255);

	if(accentRed !== lastAccentRed || accentGreen !== lastAccentGreen || accentBlue !== lastAccentBlue) {
		const accentHsv = rgbToQmkHsv(accentRed, accentGreen, accentBlue);
		setAccentBarColor(accentHsv[0], accentHsv[1]);
		lastAccentRed = accentRed;
		lastAccentGreen = accentGreen;
		lastAccentBlue = accentBlue;
	}
}

export function Shutdown() {
	// Keep the last color and avoid persisting values to the keyboard EEPROM.
}

function setKeyboardBrightness(value) {
	sendViaValue(keyboardChannel, valueBrightness, [value]);
}

function setKeyboardEffect(effect) {
	sendViaValue(keyboardChannel, valueEffect, [effect]);
}

function setKeyboardColor(hue, saturation) {
	sendViaValue(keyboardChannel, valueColor, [hue, saturation]);
}

function setAccentBarBrightness(value) {
	sendViaValue(accentBarChannel, valueBrightness, [value]);
}

function setAccentBarEffect(effect) {
	sendViaValue(accentBarChannel, valueEffect, [effect]);
}

function setAccentBarColor(hue, saturation) {
	sendViaValue(accentBarChannel, valueColor, [hue, saturation]);
}

function sendViaValue(channel, valueId, values) {
	const payload = [viaSetCustomValue, channel, valueId].concat(values);
	sendRawHidPayload(payload);
}

function sendRawHidPayload(payload) {
	const packet = new Array(33).fill(0x00);

	for(let index = 0; index < payload.length && index < 32; index++) {
		packet[index + 1] = clampByte(payload[index], 255);
	}

	device.write(packet, 33);
}

function clampByte(value, maximum) {
	if(!isFinite(value)) {
		return 0;
	}

	return Math.max(0, Math.min(maximum, Math.round(value)));
}

function rgbToQmkHsv(red, green, blue) {
	const r = red / 255;
	const g = green / 255;
	const b = blue / 255;
	const maximum = Math.max(r, g, b);
	const minimum = Math.min(r, g, b);
	const delta = maximum - minimum;
	let hueDegrees = 0;

	if(delta !== 0) {
		if(maximum === r) {
			hueDegrees = 60 * (((g - b) / delta) % 6);
		} else if(maximum === g) {
			hueDegrees = 60 * (((b - r) / delta) + 2);
		} else {
			hueDegrees = 60 * (((r - g) / delta) + 4);
		}
	}

	if(hueDegrees < 0) {
		hueDegrees += 360;
	}

	const saturation = maximum === 0 ? 0 : delta / maximum;

	return [
		clampByte((hueDegrees / 360) * 255, 255),
		clampByte(saturation * 255, 255)
	];
}

