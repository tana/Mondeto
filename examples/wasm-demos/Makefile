.PHONY: all copy clean

all: beep.wasm earth.wasm surface.wasm raygun.wasm bullet.wasm target.wasm

WASM_FILES = beep.wasm earth.wasm surface.wasm raygun.wasm bullet.wasm target.wasm
COPY_DEST = ../../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

clean:
	rm $(WASM_FILES)

ASFLAGS = --disable bulk-memory
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

beep.wasm: beep.ts

earth.wasm: earth.ts

surface.wasm: surface.ts

raygun.wasm: raygun.ts

bullet.wasm: bullet.ts

target.wasm: target.ts