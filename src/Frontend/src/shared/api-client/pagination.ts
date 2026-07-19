import { z } from 'zod';

/** Reflète PagedResult&lt;T&gt; côté backend (Products/Movements — voir Application/Common/Dtos). */
export function pagedResultSchema<T extends z.ZodTypeAny>(item: T) {
  return z.object({
    items: item.array(),
    totalCount: z.number().int(),
    page: z.number().int(),
    pageSize: z.number().int(),
  });
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
