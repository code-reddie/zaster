import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApplyRulesResult,
  CategorizationRule,
  Category,
  CreateCategorizationRule,
  CreateCategory,
} from './category.models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/category';
  private readonly rulesUrl = '/api/categorizationrule';

  getCategories() {
    return firstValueFrom(this.http.get<Category[]>(this.baseUrl));
  }

  createCategory(category: CreateCategory) {
    return firstValueFrom(this.http.post<Category>(this.baseUrl, category));
  }

  deleteCategory(id: number) {
    return firstValueFrom(this.http.delete(`${this.baseUrl}/${id}`));
  }

  getRules() {
    return firstValueFrom(this.http.get<CategorizationRule[]>(this.rulesUrl));
  }

  createRule(rule: CreateCategorizationRule) {
    return firstValueFrom(this.http.post<CategorizationRule>(this.rulesUrl, rule));
  }

  deleteRule(id: number) {
    return firstValueFrom(this.http.delete(`${this.rulesUrl}/${id}`));
  }

  applyRules() {
    return firstValueFrom(this.http.post<ApplyRulesResult>(`${this.rulesUrl}/apply`, {}));
  }
}
