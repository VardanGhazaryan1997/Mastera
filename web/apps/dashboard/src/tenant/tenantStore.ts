const STORAGE_KEY = 'mastera.tenant';

/** The salon the dashboard is working in. Read by the API client for every request. */
export interface TenantStore {
  get: () => string | null;
  set: (slug: string | null) => void;
}

function readSaved(storage: Storage | undefined): string | null {
  try {
    return storage?.getItem(STORAGE_KEY) ?? null;
  } catch {
    return null;
  }
}

export function createTenantStore(storage?: Storage): TenantStore {
  let current = readSaved(storage);
  return {
    get: () => current,
    set: (slug) => {
      current = slug;
      try {
        if (slug) storage?.setItem(STORAGE_KEY, slug);
        else storage?.removeItem(STORAGE_KEY);
      } catch {
        // Private mode or blocked storage: the choice just isn't remembered.
      }
    },
  };
}
