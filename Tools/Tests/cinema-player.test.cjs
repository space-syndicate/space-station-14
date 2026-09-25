const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const html = fs.readFileSync(path.join(__dirname, '../../Resources/Textures/Corvax/Cinema/player.html'), 'utf8');
const events = new Map();
const attempts = [];
const notifications = [];
const video = {
    readyState: 0, currentTime: 0, paused: true, seeking: false, error: null,
    addEventListener(name, callback) { events.set(name, callback); },
    load() { this.paused = true; this.readyState = 0; }, removeAttribute() {}, pause() { this.paused = true; },
    play() { this.paused = false; return new Promise((resolve, reject) => attempts.push({resolve, reject})); }
};
const context = {document: {getElementById: () => video}, window: {}, Date,
    Image: class { set src(url) { notifications.push(url); } }};
vm.runInNewContext(html.match(/<script>([\s\S]*?)<\/script>/)[1], context);
const cinema = context.window.cinema;
(async () => {
    cinema.applyState('segment:first/000000', true, 0, 0.12);
    cinema.applyState('segment:first/000000', false, 0, 0.12);
    attempts[0].reject({name: 'AbortError'});
    await new Promise(resolve => setImmediate(resolve));
    cinema.applyState('segment:first/000000', true, 0, 0.12);
    assert.equal(attempts.length, 2, 'An interrupted play must restart for the same source');
    assert.equal(notifications.length, 0, 'Downloading a blob is not proof of a decoded frame');
    video.readyState = 2;
    video.seeking = true;
    events.get('loadeddata')();
    assert.equal(notifications.length, 0);
    video.seeking = false;
    events.get('seeked')();
    events.get('canplay')();
    assert.deepEqual(notifications, ['res://localhost/cinema-ready/first/000000']);
    cinema.applyState('segment:second/000000', true, 0, 0.12);
    attempts[1].reject({name: 'AbortError'});
    await new Promise(resolve => setImmediate(resolve));
    attempts[2].resolve();
    await new Promise(resolve => setImmediate(resolve));
    video.paused = true;
    cinema.applyState('segment:second/000000', true, 0, 0.12);
    assert.equal(attempts.length, 4, 'A stale rejection must not disable the new source');
    console.log('PASS: interrupted playback resumes; readiness requires a decoded frame; stale promises are ignored');
})().catch(error => { console.error(error); process.exitCode = 1; });
