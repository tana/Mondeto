.PHONY: shooting-game

shooting-game: raygun.wasm bullet.wasm target.wasm

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

raygun.wasm: raygun.ts mondeto.ts

bullet.wasm: bullet.ts mondeto.ts

target.wasm: target.ts mondeto.ts